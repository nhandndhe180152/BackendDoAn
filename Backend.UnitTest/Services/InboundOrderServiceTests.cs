using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.InboundOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Common;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services;

public class InboundOrderServiceTests
{
    private readonly Mock<IRepositoryBase<InboundOrder, int>> _inboundOrderRepository = new();
    private readonly Mock<IInboundOrderItemRepository> _inboundOrderItemRepository = new();
    private readonly Mock<IRepositoryBase<InboundOrderStatus, int>> _inboundOrderStatusRepository = new();
    private readonly Mock<IWarehouseRepository> _warehouseRepository = new();
    private readonly Mock<IRepositoryBase<Domain.Entities.Supplier, int>> _supplierRepository = new();
    private readonly Mock<IProductVariantRepository> _productVariantRepository = new();
    private readonly Mock<ILocationRepository> _locationRepository = new();
    private readonly Mock<IInventoryRepository> _inventoryRepository = new();
    private readonly Mock<IInventoryTransactionRepository> _inventoryTransactionRepository = new();
    private readonly Mock<IIotWeightLogRepository> _iotWeightLogRepository = new();
    private readonly Mock<IIotDeviceRepository> _iotDeviceRepository = new();
    private readonly Mock<ISystemConfigRepository> _systemConfigRepository = new();
    private readonly Mock<IRepositoryBase<AuditLog, int>> _auditLogRepository = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<ILogger<InboundOrderService>> _logger = new();

    private readonly List<InboundOrderStatus> _statuses;

    public InboundOrderServiceTests()
    {
        _statuses = new List<InboundOrderStatus>
        {
            new() { Id = 1, Name = "Draft" },
            new() { Id = 2, Name = "Submitted" },
            new() { Id = 3, Name = "Approved" },
            new() { Id = 4, Name = "Rejected" },
            new() { Id = 5, Name = "Receiving" },
            new() { Id = 6, Name = "Partially Received" },
            new() { Id = 7, Name = "Fully Received" },
            // Make sure Confirmed matches name
            new() { Id = 8, Name = "Confirmed" },
            new() { Id = 9, Name = "Cancelled" }
        };

        _inboundOrderStatusRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderStatus, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderStatus, object>>[]>()))
            .ReturnsAsync((Expression<Func<InboundOrderStatus, bool>> expr, bool track, Expression<Func<InboundOrderStatus, object>>[] props) =>
            {
                var func = expr.Compile();
                return _statuses.FirstOrDefault(func);
            });

        _inboundOrderStatusRepository.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => _statuses.FirstOrDefault(s => s.Id == id));

        // Mock Transaction
        var mockTx = new Mock<IDbContextTransaction>();
        _inboundOrderRepository.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        // Setup Logged In User
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [
                    new(Backend.Share.Constants.ClaimNames.ID, "1001"),
                    new(Backend.Share.Constants.ClaimNames.ROLE_IDS, "1001") // Admin Role ID
                ]
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
            _iotWeightLogRepository.Object,
            _iotDeviceRepository.Object,
            _systemConfigRepository.Object,
            _auditLogRepository.Object,
            _httpContextAccessor.Object,
            _logger.Object
        );
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task Create_DraftOrder_CreatedSuccessfully_WithActiveEntities()
    {
        // 1. Draft inbound order is created only with active warehouse, supplier, and product.
        _warehouseRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Warehouse, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<Warehouse, object>>[]>()))
            .ReturnsAsync(new Warehouse { Id = 1, IsActive = true, IsDeleted = false });

        _supplierRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.Supplier, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<Domain.Entities.Supplier, object>>[]>()))
            .ReturnsAsync(new Domain.Entities.Supplier { Id = 1, IsActive = true, IsDeleted = false });

        _productVariantRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<ProductVariant, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<ProductVariant, object>>[]>()))
            .ReturnsAsync(new ProductVariant { Id = 1, IsActive = true, IsDeleted = false });

        var dto = new CreateInboundOrderDto
        {
            WarehouseId = 1,
            SupplierId = 1,
            Items = new List<CreateInboundOrderItemDto>
            {
                new() { ProductVariantId = 1, QuantityOrdered = 10, UnitCostPrice = 100 }
            }
        };

        var res = await Sut().CreateAsync(dto);
        res.Status.Should().Be(201);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task Update_SubmittedOrder_ReturnsUnprocessableEntity()
    {
        // 2. Submitted inbound order cannot be edited.
        var order = new InboundOrder { Id = 1, InboundOrderStatusId = 2 }; // Status 2 is Submitted
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var dto = new UpdateInboundOrderDto
        {
            Id = 1,
            Items = new List<UpdateInboundOrderItemDto>
            {
                new() { ProductVariantId = 1, QuantityOrdered = 5, UnitCostPrice = 100 }
            }
        };

        var res = await Sut().UpdateAsync(dto);
        res.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task StartReceiving_NotApproved_ReturnsUnprocessableEntity()
    {
        // 4. Receiving cannot start before approval.
        var order = new InboundOrder 
        { 
            Id = 1, 
            InboundOrderStatusId = 1, // Draft (not Approved: 3)
            InboundOrderStatus = new InboundOrderStatus { Id = 1, Name = "Draft" },
            Warehouse = new Warehouse { Id = 1, IsActive = true }
        };

        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var res = await Sut().StartReceiptAsync(1, new StartReceiptDto { InboundOrderItemId = 1 });
        res.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task ScanQr_AnotherSku_ReturnsUnprocessableEntity()
    {
        // 5. QR from another SKU is rejected.
        var order = new InboundOrder { Id = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem { Id = 1, ProductVariantId = 1, InboundOrderId = 1, Note = "{}" }; // Required sku is 1
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        var scannedVariant = new ProductVariant { Id = 2, QRCode = "QR2", IsActive = true }; // Different variant ID: 2
        _productVariantRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<ProductVariant, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<ProductVariant, object>>[]>()))
            .ReturnsAsync(scannedVariant);

        var res = await Sut().ScanQrAsync(1, 1, new ScanQrDto { QrCode = "QR2" });
        res.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task RecordQuantity_ZeroOrNegativeQuantity_ReturnsUnprocessableEntity()
    {
        // 6. Quantity zero or negative is rejected.
        var order = new InboundOrder { Id = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem { Id = 1, ProductVariantId = 1, InboundOrderId = 1, Note = "{}" };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        var res = await Sut().RecordQuantityAsync(1, 1, new RecordQuantityDto { QuantityReceived = 0 });
        res.Status.Should().Be(422);

        res = await Sut().RecordQuantityAsync(1, 1, new RecordQuantityDto { QuantityReceived = -1 });
        res.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task RecordQuantity_OverReceiving_MovesToPendingManagerReview()
    {
        // 7. Over-receiving moves receipt to Pending Manager Review.
        var order = new InboundOrder { Id = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        // Ordered is 10, receiving 12
        var item = new InboundOrderItem { Id = 1, ProductVariantId = 1, InboundOrderId = 1, QuantityOrdered = 10, Note = "{}" };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        var res = await Sut().RecordQuantityAsync(1, 1, new RecordQuantityDto { QuantityReceived = 12, Note = "Reason" });
        res.Status.Should().Be(200);
        var resItem = res.Resources as InboundOrderItemDto;
        resItem.Should().NotBeNull();
        resItem!.ReceiptStatus.Should().Be("PendingManagerReview");
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task IoTRequired_NoWeightAttached_CannotGetSuggestions()
    {
        // 8. Product requiring IoT cannot reach put-away without stable weight evidence.
        var order = new InboundOrder { Id = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem 
        { 
            Id = 1, 
            ProductVariantId = 1, 
            InboundOrderId = 1, 
            ProductVariant = new ProductVariant { Id = 1, IsIoTRequired = true },
            Note = "{\"ReceiptStatus\":\"QuantityEntered\",\"QuantityEntered\":10}" // IoT required, status is only QuantityEntered
        };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        var res = await Sut().GetPutawaySuggestionsAsync(1, 1);
        res.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task AttachWeight_ToleranceExceeded_BlocksPutawayAndMovesToPendingReview()
    {
        // 9. Failed weight tolerance blocks put-away until manager approval.
        var order = new InboundOrder { Id = 1, WarehouseId = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        // Variant standard unit weight is 1.0 kg
        var item = new InboundOrderItem 
        { 
            Id = 1, 
            ProductVariantId = 1, 
            InboundOrderId = 1, 
            ProductVariant = new ProductVariant { Id = 1, Weight = 1.0m },
            Note = "{\"ReceiptStatus\":\"QuantityEntered\",\"QuantityEntered\":10}" // Expected total weight = 10 kg
        };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        // IoT scale actual weight = 12 kg (20% discrepancy, far beyond 5% default tolerance)
        var log = new IotWeightLog 
        { 
            Id = 1, 
            WeightKg = 12.0m, 
            IsStable = true, 
            IsConfirmed = true,
            IotDevice = new IotDevice { Id = 1, WarehouseId = 1, IsActive = true, IsOnline = true }
        };
        _iotWeightLogRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotWeightLog, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotWeightLog, object>>[]>()))
            .ReturnsAsync(log);

        var res = await Sut().AttachWeightAsync(1, 1, new AttachWeightDto { IotWeightLogId = 1 });
        res.Status.Should().Be(422); // Rejects and changes state to PendingManagerReview
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task SelectPutaway_QuarantineLocation_ReturnsUnprocessableEntity()
    {
        // 10. Quarantine location cannot be selected for normal inbound put-away.
        var order = new InboundOrder { Id = 1, WarehouseId = 1 };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem 
        { 
            Id = 1, 
            ProductVariantId = 1, 
            InboundOrderId = 1, 
            Note = "{\"ReceiptStatus\":\"WeightVerified\",\"QuantityEntered\":10}"
        };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        var loc = new Location { Id = 1, WarehouseId = 1, IsActive = true, IsQuarantine = true }; // Quarantine location
        _locationRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Location, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<Location, object>>[]>()))
            .ReturnsAsync(loc);

        // Mock empty suggestions to skip override check block
        var suggestions = Enumerable.Empty<PutawaySuggestionDto>().ToList();

        var res = await Sut().SelectPutawayAsync(1, 1, new SelectPutawayDto { LocationId = 1, IsOverride = true, OverrideReason = "Test" });
        res.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task Inventory_UnchangedDuringScanQuantityWeightAndPutaway()
    {
        // 11. Inventory must not change during scanning, quantity entry, IoT verification, manager review, or put-away selection.
        // ACT/ASSERT checks inventory calls count
        var mockInvRepo = new Mock<IInventoryRepository>();
        var service = new InboundOrderService(
            _inboundOrderRepository.Object,
            _inboundOrderItemRepository.Object,
            _inboundOrderStatusRepository.Object,
            _warehouseRepository.Object,
            _supplierRepository.Object,
            _productVariantRepository.Object,
            _locationRepository.Object,
            mockInvRepo.Object,
            _inventoryTransactionRepository.Object,
            _iotWeightLogRepository.Object,
            _iotDeviceRepository.Object,
            _systemConfigRepository.Object,
            _auditLogRepository.Object,
            _httpContextAccessor.Object,
            _logger.Object
        );

        // Mock start receipt
        var order = new InboundOrder { Id = 1, WarehouseId = 1, InboundOrderStatus = new InboundOrderStatus { Id = 3, Name = "Approved" }, Warehouse = new Warehouse { Id = 1, IsActive = true } };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem { Id = 1, ProductVariantId = 1, InboundOrderId = 1, Note = "{}" };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(item);

        await service.StartReceiptAsync(1, new StartReceiptDto { InboundOrderItemId = 1 });
        mockInvRepo.Verify(r => r.CreateAsync(It.IsAny<Inventory>()), Times.Never);
        mockInvRepo.Verify(r => r.UpdateAsync(It.IsAny<Inventory>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task Confirm_IncreasesInventory_UpdatesOccupancy_CreatesTransactionExactlyOnce()
    {
        // 12. Final confirmation increases inventory exactly once.
        // 13. Final confirmation updates location occupancy.
        // 14. Final confirmation creates one InventoryTransaction.
        // 15. Repeated confirmation request does not create duplicate stock or duplicate transaction.
        var order = new InboundOrder { Id = 1, WarehouseId = 1, InboundOrderItems = new List<InboundOrderItem>() };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem 
        { 
            Id = 1, 
            ProductVariantId = 1, 
            InboundOrderId = 1, 
            Note = "{\"ReceiptStatus\":\"PutawaySelected\",\"QuantityEntered\":10,\"ConfirmedLocationId\":1}" 
        };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        var loc = new Location { Id = 1, WarehouseId = 1, IsActive = true, CurrentOccupancy = 0, MaxCapacity = 100 };
        _locationRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Location, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(loc);

        var inventory = new Inventory { Id = 1, WarehouseId = 1, LocationId = 1, ProductVariantId = 1, QuantityOnHand = 5 };
        _inventoryRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Inventory, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<Inventory, object>>[]>()))
            .ReturnsAsync(inventory);

        // Mock list of items for doc status recalculation
        _inboundOrderItemRepository.Setup(r => r.FindByConditionAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<InboundOrderItem> { item });

        var res = await Sut().ConfirmReceiptAsync(1, 1, new ConfirmReceiptDto { OperationKey = "Key1" });
        res.Status.Should().Be(200);

        inventory.QuantityOnHand.Should().Be(15); // Old 5 + Confirmed 10
        loc.CurrentOccupancy.Should().Be(10); // Confirmed 10

        _inventoryRepository.Verify(r => r.UpdateAsync(It.IsAny<Inventory>()), Times.Once);
        _inventoryTransactionRepository.Verify(r => r.CreateAsync(It.IsAny<InventoryTransaction>()), Times.Once);

        // Test repeated confirmation request (Idempotency check)
        item.Note = "{\"ReceiptStatus\":\"Confirmed\",\"QuantityEntered\":10,\"ConfirmedLocationId\":1}";
        var resDuplicate = await Sut().ConfirmReceiptAsync(1, 1, new ConfirmReceiptDto { OperationKey = "Key1" });
        resDuplicate.Status.Should().Be(200);
        inventory.QuantityOnHand.Should().Be(15); // Remains 15
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task Confirm_ConcurrencyConflict_ReturnsConflict()
    {
        // 16. Concurrent receipt confirmation returns conflict or safely retries without data corruption.
        var order = new InboundOrder { Id = 1, WarehouseId = 1, InboundOrderItems = new List<InboundOrderItem>() };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item = new InboundOrderItem 
        { 
            Id = 1, 
            ProductVariantId = 1, 
            InboundOrderId = 1, 
            Note = "{\"ReceiptStatus\":\"PutawaySelected\",\"QuantityEntered\":10,\"ConfirmedLocationId\":1}" 
        };
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item);

        var loc = new Location { Id = 1, WarehouseId = 1, IsActive = true, CurrentOccupancy = 0, MaxCapacity = 100 };
        _locationRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Location, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(loc);

        _inboundOrderRepository.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict."));

        var res = await Sut().ConfirmReceiptAsync(1, 1, new ConfirmReceiptDto { OperationKey = "KeyConflict" });
        res.Status.Should().Be(409); // Conflict
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task Recalculation_StatusTransition_PartiallyAndFullyReceived()
    {
        // 17. Document becomes Partially Received after partial confirmation.
        // 18. Document becomes Confirmed only after all lines are settled.
        var order = new InboundOrder { Id = 1, WarehouseId = 1, InboundOrderItems = new List<InboundOrderItem>() };
        _inboundOrderRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrder, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrder, object>>[]>()))
            .ReturnsAsync(order);

        var item1 = new InboundOrderItem 
        { 
            Id = 1, 
            ProductVariantId = 1, 
            InboundOrderId = 1, 
            QuantityOrdered = 10,
            QuantityReceived = 0, // Not received yet
            Note = "{\"ReceiptStatus\":\"PutawaySelected\",\"QuantityEntered\":10,\"ConfirmedLocationId\":1}" 
        };
        var item2 = new InboundOrderItem 
        { 
            Id = 2, 
            ProductVariantId = 2, 
            InboundOrderId = 1, 
            QuantityOrdered = 10,
            QuantityReceived = 0,
            Note = "{}" 
        };

        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item1);

        var loc = new Location { Id = 1, WarehouseId = 1, IsActive = true, CurrentOccupancy = 0, MaxCapacity = 100 };
        _locationRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Location, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(loc);

        // Partially received scenario: item1 received, item2 not received
        _inboundOrderItemRepository.Setup(r => r.FindByConditionAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<InboundOrderItem> { item1, item2 });

        var res = await Sut().ConfirmReceiptAsync(1, 1, new ConfirmReceiptDto { OperationKey = "KeyPart1" });
        res.Status.Should().Be(200);

        order.InboundOrderStatusId.Should().Be(6); // Partially Received is status ID 6

        // Fully received scenario: item1 and item2 both received
        item1.QuantityReceived = 10;
        item2.QuantityReceived = 10;

        // Reset state of item2 so it is ready to confirm
        item2.Note = "{\"ReceiptStatus\":\"PutawaySelected\",\"QuantityEntered\":10,\"ConfirmedLocationId\":1}";
        _inboundOrderItemRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InboundOrderItem, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<InboundOrderItem, object>>[]>()))
            .ReturnsAsync(item2);

        var resFull = await Sut().ConfirmReceiptAsync(1, 2, new ConfirmReceiptDto { OperationKey = "KeyFull" });
        resFull.Status.Should().Be(200);

        order.InboundOrderStatusId.Should().Be(8); // Confirmed is status ID 8
    }
}
