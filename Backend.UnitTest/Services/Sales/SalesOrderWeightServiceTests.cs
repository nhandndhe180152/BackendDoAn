using Backend.Application.Constants;
using Backend.Application.DTOs.IotWeights;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Backend.UnitTest.Services.Sales;

public class SalesOrderWeightServiceTests
{
    private readonly Mock<IIotDeviceRepository> _iotDeviceRepository = new();
    private readonly Mock<IIotWeightLogRepository> _iotWeightLogRepository = new();
    private readonly Mock<IProductVariantRepository> _productVariantRepository = new();
    private readonly Mock<IInboundOrderItemRepository> _inboundOrderItemRepository = new();
    private readonly Mock<IOutboundOrderItemRepository> _outboundOrderItemRepository = new();
    private readonly Mock<IStockTakeItemRepository> _stockTakeItemRepository = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<IDbContextTransaction> _transaction = new();

    private readonly IotWeightService _sut;

    public SalesOrderWeightServiceTests()
    {
        _transaction
            .Setup(transaction => transaction.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _iotWeightLogRepository
            .Setup(repo => repo.BeginTransactionAsync())
            .ReturnsAsync(_transaction.Object);

        _sut = new IotWeightService(
            _iotDeviceRepository.Object,
            _iotWeightLogRepository.Object,
            _productVariantRepository.Object,
            _inboundOrderItemRepository.Object,
            _outboundOrderItemRepository.Object,
            _stockTakeItemRepository.Object,
            _httpContextAccessor.Object);
    }

    [Fact]
    [Trait("Service", "Sales")]
    [Trait("Method", "AttachSalesOrderWeight")]
    public async Task AttachContextAsync_SalesOrder_UpdatesActualWeightAndConfirmsLog()
    {
        // Arrange
        var log = CreateStableWeightLog(id: 10, weightKg: 2.35m);
        var item = CreateOutboundOrderItem(id: 20, outboundOrderId: 30, productVariantId: 40);

        SetupAttachSaleData(log, item, productVariantId: 40);

        var request = new AttachIotWeightContextDto
        {
            ReferenceType = IotWeightReferenceTypeConstants.OutboundOrder,
            ReferenceId = 30,
            ReferenceItemId = 20,
            ProductVariantId = 40,
            UpdateReferenceItemActualWeight = true
        };

        // Act
        var response = await _sut.AttachContextAsync(10, request);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<AttachedIotWeightContextDto>().Subject;
        result.ReferenceType.Should().Be(IotWeightReferenceTypeConstants.OutboundOrder);
        result.ReferenceId.Should().Be(30);
        result.ReferenceItemId.Should().Be(20);
        result.ProductVariantId.Should().Be(40);
        result.ReferenceItemActualWeightKg.Should().Be(2.35m);

        log.IsConfirmed.Should().BeTrue();
        log.ReferenceType.Should().Be(IotWeightReferenceTypeConstants.OutboundOrder);
        item.ActualWeightKg.Should().Be(2.35m);

        _outboundOrderItemRepository.Verify(repo => repo.UpdateAsync(item), Times.Once);
        _iotWeightLogRepository.Verify(repo => repo.UpdateAsync(log), Times.Once);
        _iotWeightLogRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        _iotWeightLogRepository.Verify(repo => repo.EndTransactionAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Sales")]
    [Trait("Method", "AttachSalesOrderWeight")]
    public async Task AttachContextAsync_SalesOrder_WhenNotUpdateItem_DoesNotUpdateActualWeight()
    {
        // Arrange
        var log = CreateStableWeightLog(id: 11, weightKg: 1.25m);
        var item = CreateOutboundOrderItem(id: 21, outboundOrderId: 31, productVariantId: 41, actualWeightKg: 0.9m);

        SetupAttachSaleData(log, item, productVariantId: 41);

        var request = new AttachIotWeightContextDto
        {
            ReferenceType = "SO",
            ReferenceId = 31,
            ReferenceItemId = 21,
            ProductVariantId = 41,
            UpdateReferenceItemActualWeight = false
        };

        // Act
        var response = await _sut.AttachContextAsync(11, request);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<AttachedIotWeightContextDto>().Subject;
        result.ReferenceType.Should().Be(IotWeightReferenceTypeConstants.OutboundOrder);
        result.ReferenceItemActualWeightKg.Should().Be(0.9m);
        item.ActualWeightKg.Should().Be(0.9m);

        _outboundOrderItemRepository.Verify(repo => repo.UpdateAsync(It.IsAny<OutboundOrderItem>()), Times.Never);
        _iotWeightLogRepository.Verify(repo => repo.UpdateAsync(log), Times.Once);
        _iotWeightLogRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Sales")]
    [Trait("Method", "AttachSalesOrderWeight")]
    public async Task AttachContextAsync_SalesOrder_MissingOutboundOrderItem_ReturnsNotFound()
    {
        // Arrange
        var log = CreateStableWeightLog(id: 12, weightKg: 3m);

        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(12)).ReturnsAsync(log);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(42)).ReturnsAsync(CreateProductVariant(42));
        _outboundOrderItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(22)).ReturnsAsync((OutboundOrderItem?)null);

        var request = new AttachIotWeightContextDto
        {
            ReferenceType = IotWeightReferenceTypeConstants.OutboundOrder,
            ReferenceId = 32,
            ReferenceItemId = 22,
            ProductVariantId = 42
        };

        // Act
        var response = await _sut.AttachContextAsync(12, request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
        _iotWeightLogRepository.Verify(repo => repo.RollbackTransactionAsync(), Times.Once);
        _iotWeightLogRepository.Verify(repo => repo.UpdateAsync(It.IsAny<IotWeightLog>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Sales")]
    [Trait("Method", "AttachSalesOrderWeight")]
    public async Task AttachContextAsync_SalesOrder_ItemBelongsToDifferentOrder_ReturnsBadRequest()
    {
        // Arrange
        var log = CreateStableWeightLog(id: 13, weightKg: 4m);
        var item = CreateOutboundOrderItem(id: 23, outboundOrderId: 999, productVariantId: 43);

        SetupAttachSaleData(log, item, productVariantId: 43);

        var request = new AttachIotWeightContextDto
        {
            ReferenceType = IotWeightReferenceTypeConstants.OutboundOrder,
            ReferenceId = 33,
            ReferenceItemId = 23,
            ProductVariantId = 43
        };

        // Act
        var response = await _sut.AttachContextAsync(13, request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _iotWeightLogRepository.Verify(repo => repo.RollbackTransactionAsync(), Times.Once);
        _outboundOrderItemRepository.Verify(repo => repo.UpdateAsync(It.IsAny<OutboundOrderItem>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Sales")]
    [Trait("Method", "AttachSalesOrderWeight")]
    public async Task AttachContextAsync_SalesOrder_ItemProductVariantMismatch_ReturnsBadRequest()
    {
        // Arrange
        var log = CreateStableWeightLog(id: 14, weightKg: 5m);
        var item = CreateOutboundOrderItem(id: 24, outboundOrderId: 34, productVariantId: 999);

        SetupAttachSaleData(log, item, productVariantId: 44);

        var request = new AttachIotWeightContextDto
        {
            ReferenceType = IotWeightReferenceTypeConstants.OutboundOrder,
            ReferenceId = 34,
            ReferenceItemId = 24,
            ProductVariantId = 44
        };

        // Act
        var response = await _sut.AttachContextAsync(14, request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _iotWeightLogRepository.Verify(repo => repo.RollbackTransactionAsync(), Times.Once);
        _outboundOrderItemRepository.Verify(repo => repo.UpdateAsync(It.IsAny<OutboundOrderItem>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Sales")]
    [Trait("Method", "AttachSalesOrderWeight")]
    public async Task AttachContextAsync_SalesOrder_MissingProductVariant_ReturnsNotFound()
    {
        // Arrange
        var log = CreateStableWeightLog(id: 15, weightKg: 2m);

        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(15)).ReturnsAsync(log);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(45)).ReturnsAsync((ProductVariant?)null);

        var request = new AttachIotWeightContextDto
        {
            ReferenceType = IotWeightReferenceTypeConstants.OutboundOrder,
            ReferenceId = 35,
            ReferenceItemId = 25,
            ProductVariantId = 45
        };

        // Act
        var response = await _sut.AttachContextAsync(15, request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
        _outboundOrderItemRepository.Verify(repo => repo.GetByIdForWeightAttachAsync(It.IsAny<int>()), Times.Never);
        _iotWeightLogRepository.Verify(repo => repo.UpdateAsync(It.IsAny<IotWeightLog>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Sales")]
    [Trait("Method", "AttachSalesOrderWeight")]
    public async Task AttachContextAsync_SalesOrder_UnstableWeightLog_ReturnsBadRequest()
    {
        // Arrange
        var log = CreateStableWeightLog(id: 16, weightKg: 2m);
        log.IsStable = false;

        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(16)).ReturnsAsync(log);

        var request = new AttachIotWeightContextDto
        {
            ReferenceType = IotWeightReferenceTypeConstants.OutboundOrder,
            ReferenceId = 36,
            ReferenceItemId = 26,
            ProductVariantId = 46
        };

        // Act
        var response = await _sut.AttachContextAsync(16, request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _productVariantRepository.Verify(repo => repo.GetActiveByIdAsync(It.IsAny<int>()), Times.Never);
        _iotWeightLogRepository.Verify(repo => repo.UpdateAsync(It.IsAny<IotWeightLog>()), Times.Never);
    }

    private void SetupAttachSaleData(IotWeightLog log, OutboundOrderItem item, int productVariantId)
    {
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(log.Id)).ReturnsAsync(log);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(productVariantId)).ReturnsAsync(CreateProductVariant(productVariantId));
        _outboundOrderItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(item.Id)).ReturnsAsync(item);
        _outboundOrderItemRepository.Setup(repo => repo.UpdateAsync(It.IsAny<OutboundOrderItem>())).Returns(Task.CompletedTask);
        _iotWeightLogRepository.Setup(repo => repo.UpdateAsync(It.IsAny<IotWeightLog>())).Returns(Task.CompletedTask);
        _iotWeightLogRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _iotWeightLogRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);
        _iotWeightLogRepository.Setup(repo => repo.RollbackTransactionAsync()).Returns(Task.CompletedTask);
    }

    private static IotWeightLog CreateStableWeightLog(int id, decimal weightKg)
    {
        return new IotWeightLog
        {
            Id = id,
            IoTDeviceId = 1,
            IotDevice = new IotDevice
            {
                Id = 1,
                DeviceCode = "SCALE-01",
                DeviceName = "Can kho",
                DeviceType = "SCALE",
                ApiKeyHash = "hash",
                IsActive = true
            },
            WeightKg = weightKg,
            Unit = "kg",
            IsStable = true,
            IsConfirmed = false,
            MeasuredAt = DateTime.Now.AddMinutes(-1),
            ReceivedAt = DateTime.Now
        };
    }

    private static OutboundOrderItem CreateOutboundOrderItem(
        int id,
        int outboundOrderId,
        int productVariantId,
        decimal? actualWeightKg = null)
    {
        return new OutboundOrderItem
        {
            Id = id,
            OutboundOrderId = outboundOrderId,
            ProductVariantId = productVariantId,
            QuantityOrdered = 5,
            QuantityPicked = 0,
            UnitCostPrice = 100000,
            ActualWeightKg = actualWeightKg,
            IsDeleted = false
        };
    }

    private static ProductVariant CreateProductVariant(int id)
    {
        return new ProductVariant
        {
            Id = id,
            SKU = $"SKU-{id}",
            Name = $"Variant {id}",
            ProductId = 1,
            UnitOfMeasureId = 1,
            IsActive = true,
            IsDeleted = false
        };
    }
}
