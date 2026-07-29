using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.InboundOrders;
using Backend.Application.DTOs.Putaway;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Abstractions.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services;

[Trait("Service", "InboundOrder")]
public class InboundOrderServiceTests
{
    private readonly Mock<IRepositoryBase<InboundOrder, int>> _inboundOrderRepository = new();
    private readonly Mock<IInboundOrderItemRepository> _inboundOrderItemRepository = new();
    private readonly Mock<IRepositoryBase<InboundOrderStatus, int>> _inboundOrderStatusRepository = new();
    private readonly Mock<IWarehouseRepository> _warehouseRepository = new();
    private readonly Mock<IRepositoryBase<Backend.Domain.Entities.Supplier, int>> _supplierRepository = new();
    private readonly Mock<IProductVariantRepository> _productVariantRepository = new();
    private readonly Mock<ILocationRepository> _locationRepository = new();
    private readonly Mock<IInventoryRepository> _inventoryRepository = new();
    private readonly Mock<IInventoryTransactionRepository> _inventoryTransactionRepository = new();
    private readonly Mock<ISystemConfigRepository> _systemConfigRepository = new();
    private readonly Mock<IRepositoryBase<DeliveryNote, int>> _deliveryNoteRepository = new();
    private readonly Mock<IRepositoryBase<FileUpload, int>> _fileUploadRepository = new();
    private readonly Mock<IStorageService> _storageService = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<ILogger<InboundOrderService>> _logger = new();
    private readonly Mock<INotificationDispatcher> _notificationDispatcher = new();
    private readonly Mock<IRepositoryBase<PaddyPurchaseReceipt, int>> _paddyPurchaseReceiptRepository = new();
    private readonly Mock<IRepositoryBase<Backend.Domain.Entities.PaddyPurchaseSchedule, int>> _paddyPurchaseScheduleRepository = new();
    private readonly Mock<ISystemLookup> _systemLookup = new();
    private readonly Mock<IRepositoryBase<PurchaseOrder, int>> _purchaseOrderRepository = new();
    private readonly Mock<IRepositoryBase<PurchaseOrderStatus, int>> _purchaseOrderStatusRepository = new();
    private readonly Mock<IRepositoryBase<Backend.Domain.Entities.PaddyLot, int>> _paddyLotRepository = new();
    private readonly Mock<IRepositoryBase<LotStatus, int>> _lotStatusRepository = new();
    private readonly Mock<IPaddyPurchaseReceiptService> _paddyPurchaseReceiptService = new();

    public InboundOrderServiceTests()
    {
        // Mock default database transaction
        var mockTx = new Mock<IDbContextTransaction>();
        _inboundOrderRepository.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        // Mock logged in user
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(Backend.Share.Constants.ClaimNames.ID, "1001") }
            )
        );
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
    }

    private InboundOrderService Sut()
    {
        return new InboundOrderService(
            _inboundOrderRepository.Object,
            _inboundOrderItemRepository.Object,
            _inboundOrderStatusRepository.Object,
            _warehouseRepository.Object,
            _supplierRepository.Object,
            _productVariantRepository.Object,
            _locationRepository.Object,
            _inventoryRepository.Object,
            _inventoryTransactionRepository.Object,
            _systemConfigRepository.Object,
            _deliveryNoteRepository.Object,
            _fileUploadRepository.Object,
            _storageService.Object,
            _httpContextAccessor.Object,
            _logger.Object,
            _notificationDispatcher.Object,
            _paddyPurchaseReceiptRepository.Object,
            _paddyPurchaseScheduleRepository.Object,
            _systemLookup.Object,
            _purchaseOrderRepository.Object,
            _purchaseOrderStatusRepository.Object,
            _paddyLotRepository.Object,
            _lotStatusRepository.Object,
            _paddyPurchaseReceiptService.Object
        );
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        var orders = new List<InboundOrder>();
        _inboundOrderRepository.Setup(r => r.FindByCondition(
            It.IsAny<Expression<Func<InboundOrder, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .Returns(orders.AsQueryable().BuildMock());

        var result = await Sut().GetByIdAsync(999);
        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task CreateAsync_NoItems_Returns422()
    {
        var warehouse = new Backend.Domain.Entities.Warehouse { Id = 1, IsActive = true, IsDeleted = false };
        _warehouseRepository.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, object>>[]>()))
            .ReturnsAsync(warehouse);

        var dto = new CreateInboundOrderDto
        {
            WarehouseId = 1,
            SupplierId = 1,
            Items = new List<CreateInboundOrderItemDto>()
        };

        var result = await Sut().CreateAsync(dto);
        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task GetPutawaySuggestionsAsync_InboundOrderNotFound_Returns404()
    {
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<InboundOrder, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync((InboundOrder?)null);

        var result = await Sut().GetPutawaySuggestionsAsync(1, 1);
        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task GetPutawaySuggestionsAsync_ReceiptItemNotFound_Returns404()
    {
        var order = new InboundOrder { Id = 1, WarehouseId = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<InboundOrder, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<InboundOrderItem, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync((InboundOrderItem?)null);

        var result = await Sut().GetPutawaySuggestionsAsync(1, 999);
        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task GetPutawaySuggestionsAsync_HasCandidateLocations_ScoresThemWithCorrectWeights()
    {
        // Arrange
        var order = new InboundOrder { Id = 1, WarehouseId = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<InboundOrder, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem
        {
            Id = 2,
            InboundOrderId = 1,
            ProductVariantId = 10,
            ProductVariant = new ProductVariant
            {
                Id = 10,
                Product = new Domain.Entities.Product { ProductCategoryId = 5 }
            },
            Note = "{\"ReceiptStatus\":\"QuantityEntered\",\"QuantityEntered\":1000}" // 1000 kg entered
        };

        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<InboundOrderItem, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        // Candidate location that allows this category
        // MaxCapacity = 2000, CurrentOccupancy = 500, AvailableCapacity = 1500. Priority = 50.
        var loc = new Location
        {
            Id = 101,
            WarehouseId = 1,
            IsActive = true,
            IsDeleted = false,
            IsQuarantine = false,
            MaxCapacity = 2000,
            CurrentOccupancy = 500,
            AllowedCategoryId = 5,
            Priority = 50,
            ZoneName = "Zone A",
            SlotCode = "LOC-101"
        };

        var candidates = new List<Location> { loc };
        _locationRepository.Setup(r => r.FindByConditionAsync(
            It.IsAny<Expression<Func<Location, bool>>>(),
            It.IsAny<bool>()))
            .ReturnsAsync(candidates);

        // Mock SystemConfig key retrieval to fall back to default weights
        _systemConfigRepository.Setup(r => r.GetValueByKey(It.IsAny<string>()))
            .ReturnsAsync("");

        // Act
        var result = await Sut().GetPutawaySuggestionsAsync(1, 2);

        // Assert
        result.Status.Should().Be(200);
        var response = result.Resources.Should().BeOfType<List<Backend.Application.DTOs.InboundOrders.PutawaySuggestionDto>>().Subject;
        response.Should().HaveCount(1);

        var suggestion = response[0];
        suggestion.LocationId.Should().Be(101);

        // Calculate expected score:
        // catMatchW = 0.20
        // capFitW = 0.40
        // occW = 0.30
        // priW = 0.10
        //
        // Category Match: loc.AllowedCategoryId == 5, so catScore = 1.00
        // Capacity Fit: receiptQty = 1000, availCap = 1500
        //               fitScore = Min(1000, 1500) / Max(1000, 1500) = 1000 / 1500 = 0.66666667
        // Occupancy: occScore = 1.0 - (500 / 2000) = 0.75
        // Priority: pScore = 50 / 100.0 = 0.50
        //
        // Expected finalScore = (0.20 * 1.00) + (0.40 * 0.66666667) + (0.30 * 0.75) + (0.10 * 0.50)
        //                     = 0.20 + 0.26666667 + 0.225 + 0.05
        //                     = 0.74166667
        // Score is rounded to 4 decimals -> 0.7417
        suggestion.Score.Should().Be(0.7417);
    }
}
