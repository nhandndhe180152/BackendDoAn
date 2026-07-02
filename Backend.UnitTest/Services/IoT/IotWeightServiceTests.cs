using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotWeights;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.IoT;

public class IotWeightServiceTests
{
    private readonly Mock<IIotDeviceRepository> _iotDeviceRepository = new();
    private readonly Mock<IIotWeightLogRepository> _iotWeightLogRepository = new();
    private readonly Mock<IProductVariantRepository> _productVariantRepository = new();
    private readonly Mock<IInboundOrderItemRepository> _inboundOrderItemRepository = new();
    private readonly Mock<IOutboundOrderItemRepository> _outboundOrderItemRepository = new();
    private readonly Mock<IStockTakeItemRepository> _stockTakeItemRepository = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly IotWeightService _sut;

    public IotWeightServiceTests()
    {
        _sut = new IotWeightService(
            _iotDeviceRepository.Object,
            _iotWeightLogRepository.Object,
            _productVariantRepository.Object,
            _inboundOrderItemRepository.Object,
            _outboundOrderItemRepository.Object,
            _stockTakeItemRepository.Object,
            _httpContextAccessor.Object
        );
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "ReceiveWeight")]
    public async Task ReceiveWeightAsync_WhenDeviceNotExists_ReturnsUnauthorized()
    {
        // Arrange
        var dto = new ReceiveIotWeightDto { DeviceCode = "DEV01", Weight = 1.5m };
        _iotDeviceRepository
            .Setup(repo => repo.GetActiveByDeviceCodeAsync("DEV01"))
            .ReturnsAsync((IotDevice?)null);

        // Act
        var response = await _sut.ReceiveWeightAsync(dto, null, "127.0.0.1");

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(401);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "ReceiveWeight")]
    public async Task ReceiveWeightAsync_WhenKeyInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var dto = new ReceiveIotWeightDto { DeviceCode = "DEV01", Weight = 1.5m };
        var device = new IotDevice { Id = 1, ApiKeyHash = DeviceKeyHelper.HashKey("correct-key") };
        _iotDeviceRepository
            .Setup(repo => repo.GetActiveByDeviceCodeAsync("DEV01"))
            .ReturnsAsync(device);

        // Act
        var response = await _sut.ReceiveWeightAsync(dto, "wrong-key", "127.0.0.1");

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(401);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "ReceiveWeight")]
    public async Task ReceiveWeightAsync_WhenWeightNegative_ReturnsBadRequest()
    {
        // Arrange
        var dto = new ReceiveIotWeightDto { DeviceCode = "DEV01", Weight = -1.5m };
        var device = new IotDevice { Id = 1, ApiKeyHash = DeviceKeyHelper.HashKey("key") };
        _iotDeviceRepository
            .Setup(repo => repo.GetActiveByDeviceCodeAsync("DEV01"))
            .ReturnsAsync(device);

        // Act
        var response = await _sut.ReceiveWeightAsync(dto, "key", "127.0.0.1");

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "ReceiveWeight")]
    public async Task ReceiveWeightAsync_WhenValid_CreatesAndSaves()
    {
        // Arrange
        var dto = new ReceiveIotWeightDto { DeviceCode = "DEV01", Weight = 15.5m };
        var device = new IotDevice { Id = 1, ApiKeyHash = DeviceKeyHelper.HashKey("key") };
        _iotDeviceRepository
            .Setup(repo => repo.GetActiveByDeviceCodeAsync("DEV01"))
            .ReturnsAsync(device);

        _iotWeightLogRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<IotWeightLog>()))
            .Returns(Task.CompletedTask);

        _iotDeviceRepository
            .Setup(repo => repo.UpdateAsync(device))
            .Returns(Task.CompletedTask);

        _iotWeightLogRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.ReceiveWeightAsync(dto, "key", "127.0.0.1");

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        _iotWeightLogRepository.Verify(repo => repo.CreateAsync(It.IsAny<IotWeightLog>()), Times.Once);
        _iotWeightLogRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "GetLatestWeight")]
    public async Task GetLatestWeightAsync_WhenDeviceNotFound_ReturnsNotFound()
    {
        // Arrange
        _iotDeviceRepository
            .Setup(repo => repo.GetActiveByDeviceCodeAsync("DEV01"))
            .ReturnsAsync((IotDevice?)null);

        // Act
        var response = await _sut.GetLatestWeightAsync("DEV01");

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenLogConfirmed_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = true, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository
            .Setup(repo => repo.GetByIdForAttachAsync(1))
            .ReturnsAsync(log);

        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository
            .Setup(repo => repo.BeginTransactionAsync())
            .ReturnsAsync(mockTx.Object);

        var dto = new AttachIotWeightContextDto { ReferenceType = "MANUAL" };

        // Act
        var response = await _sut.AttachContextAsync(1, dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "GetLatestWeight")]
    public async Task GetLatestWeightAsync_WhenDeviceCodeEmpty_ReturnsBadRequest()
    {
        // Act
        var response = await _sut.GetLatestWeightAsync("   ");

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "GetLatestWeight")]
    public async Task GetLatestWeightAsync_WhenNoWeightLog_ReturnsNotFound()
    {
        // Arrange
        var device = new IotDevice { Id = 1, DeviceCode = "DEV01" };
        _iotDeviceRepository
            .Setup(repo => repo.GetActiveByDeviceCodeAsync("DEV01"))
            .ReturnsAsync(device);
        _iotWeightLogRepository
            .Setup(repo => repo.GetLatestByDeviceIdAsync(1))
            .ReturnsAsync((IotWeightLog?)null);

        // Act
        var response = await _sut.GetLatestWeightAsync("DEV01");

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "GetLatestWeight")]
    public async Task GetLatestWeightAsync_WhenValid_ReturnsLatestWeight()
    {
        // Arrange
        var device = new IotDevice { Id = 1, DeviceCode = "DEV01" };
        var log = new IotWeightLog { Id = 10, IoTDeviceId = 1, WeightKg = 12.5m, CreatedDate = DateTime.Now };
        _iotDeviceRepository
            .Setup(repo => repo.GetActiveByDeviceCodeAsync("DEV01"))
            .ReturnsAsync(device);
        _iotWeightLogRepository
            .Setup(repo => repo.GetLatestByDeviceIdAsync(1))
            .ReturnsAsync(log);

        // Act
        var response = await _sut.GetLatestWeightAsync("DEV01");

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenLogNotStable_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = false, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto { ReferenceType = "MANUAL" });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenWeightZeroOrNegative_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 0 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto { ReferenceType = "MANUAL" });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenReferenceTypeInvalid_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto { ReferenceType = "INVALID" });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenManual_SavesSuccessfully()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _iotWeightLogRepository.Setup(repo => repo.UpdateAsync(log)).Returns(Task.CompletedTask);
        _iotWeightLogRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _iotWeightLogRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto { ReferenceType = "MANUAL" });

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        log.IsConfirmed.Should().BeTrue();
        log.ReferenceType.Should().Be("MANUAL");
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenNotManualAndVariantIdNull_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto { ReferenceType = "PURCHASE_ORDER", ProductVariantId = null });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenVariantNotFound_ReturnsNotFound()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync((ProductVariant?)null);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto { ReferenceType = "PURCHASE_ORDER", ProductVariantId = 99 });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenPurchaseOrderMissingIds_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync(new ProductVariant { Id = 99 });

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto 
        { 
            ReferenceType = "PURCHASE_ORDER", 
            ProductVariantId = 99,
            ReferenceId = null 
        });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenInboundOrderItemNotFound_ReturnsNotFound()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync(new ProductVariant { Id = 99 });
        _inboundOrderItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(200)).ReturnsAsync((InboundOrderItem?)null);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto 
        { 
            ReferenceType = "PURCHASE_ORDER", 
            ProductVariantId = 99,
            ReferenceId = 10,
            ReferenceItemId = 200
        });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenInboundOrderItemOrderIdMismatch_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync(new ProductVariant { Id = 99 });
        
        var poItem = new InboundOrderItem { Id = 200, InboundOrderId = 999 }; // 999 != 10
        _inboundOrderItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(200)).ReturnsAsync(poItem);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto 
        { 
            ReferenceType = "PURCHASE_ORDER", 
            ProductVariantId = 99,
            ReferenceId = 10,
            ReferenceItemId = 200
        });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenInboundOrderItemVariantIdMismatch_ReturnsBadRequest()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync(new ProductVariant { Id = 99 });
        
        var poItem = new InboundOrderItem { Id = 200, InboundOrderId = 10, ProductVariantId = 111 }; // 111 != 99
        _inboundOrderItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(200)).ReturnsAsync(poItem);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto 
        { 
            ReferenceType = "PURCHASE_ORDER", 
            ProductVariantId = 99,
            ReferenceId = 10,
            ReferenceItemId = 200
        });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenPurchaseOrderValidAndUpdatesWeight_SavesSuccessfully()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync(new ProductVariant { Id = 99 });
        
        var poItem = new InboundOrderItem { Id = 200, InboundOrderId = 10, ProductVariantId = 99 };
        _inboundOrderItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(200)).ReturnsAsync(poItem);
        _inboundOrderItemRepository.Setup(repo => repo.UpdateAsync(poItem)).Returns(Task.CompletedTask);

        _iotWeightLogRepository.Setup(repo => repo.UpdateAsync(log)).Returns(Task.CompletedTask);
        _iotWeightLogRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _iotWeightLogRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto 
        { 
            ReferenceType = "PURCHASE_ORDER", 
            ProductVariantId = 99,
            ReferenceId = 10,
            ReferenceItemId = 200,
            UpdateReferenceItemActualWeight = true
        });

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        poItem.ActualWeightKg.Should().Be(5);
        _inboundOrderItemRepository.Verify(repo => repo.UpdateAsync(poItem), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenSalesOrderValidAndUpdatesWeight_SavesSuccessfully()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync(new ProductVariant { Id = 99 });
        
        var soItem = new OutboundOrderItem { Id = 300, OutboundOrderId = 11, ProductVariantId = 99 };
        _outboundOrderItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(300)).ReturnsAsync(soItem);
        _outboundOrderItemRepository.Setup(repo => repo.UpdateAsync(soItem)).Returns(Task.CompletedTask);

        _iotWeightLogRepository.Setup(repo => repo.UpdateAsync(log)).Returns(Task.CompletedTask);
        _iotWeightLogRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _iotWeightLogRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto 
        { 
            ReferenceType = "SALES_ORDER", 
            ProductVariantId = 99,
            ReferenceId = 11,
            ReferenceItemId = 300,
            UpdateReferenceItemActualWeight = true
        });

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        soItem.ActualWeightKg.Should().Be(5);
        _outboundOrderItemRepository.Verify(repo => repo.UpdateAsync(soItem), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenStockTakeValid_SavesSuccessfully()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ReturnsAsync(new ProductVariant { Id = 99 });
        
        var stItem = new StockTakeItem { Id = 400, StockTakeId = 12, ProductVariantId = 99 };
        _stockTakeItemRepository.Setup(repo => repo.GetByIdForWeightAttachAsync(400)).ReturnsAsync(stItem);

        _iotWeightLogRepository.Setup(repo => repo.UpdateAsync(log)).Returns(Task.CompletedTask);
        _iotWeightLogRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _iotWeightLogRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto 
        { 
            ReferenceType = "STOCK_TAKE", 
            ProductVariantId = 99,
            ReferenceId = 12,
            ReferenceItemId = 400
        });

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        log.IsConfirmed.Should().BeTrue();
        log.ReferenceType.Should().Be("STOCK_TAKE");
    }

    [Fact]
    [Trait("Service", "IotWeight")]
    [Trait("Method", "AttachContext")]
    public async Task AttachContextAsync_WhenExceptionOccurs_RollsBackAndReturnsInternalServerError()
    {
        // Arrange
        var log = new IotWeightLog { Id = 1, IsConfirmed = false, IsStable = true, WeightKg = 5 };
        _iotWeightLogRepository.Setup(repo => repo.GetByIdForAttachAsync(1)).ReturnsAsync(log);
        var mockTx = new Mock<IDbContextTransaction>();
        _iotWeightLogRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(99)).ThrowsAsync(new Exception("DB connection error"));
        _iotWeightLogRepository.Setup(repo => repo.RollbackTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.AttachContextAsync(1, new AttachIotWeightContextDto { ReferenceType = "PURCHASE_ORDER", ProductVariantId = 99 });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(500);
        _iotWeightLogRepository.Verify(repo => repo.RollbackTransactionAsync(), Times.Once);
    }
}
