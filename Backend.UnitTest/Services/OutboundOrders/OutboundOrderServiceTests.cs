using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.OutboundOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using MockQueryable.Moq;
using Xunit;
using System.Reflection;
using PaddyLotEntity = Backend.Domain.Entities.PaddyLot;

namespace Backend.UnitTest.Services.OutboundOrders;

[Trait("Service", "OutboundOrder")]
public class OutboundOrderServiceTests
{
    private readonly Mock<IOutboundOrderRepository> _obRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderStatus, int>> _obStatusRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderItemAllocation, int>> _allocRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<IInventoryTransactionRepository> _invTxRepo = new();
    private readonly Mock<ISalesOrderRepository> _soRepo = new();
    private readonly Mock<IRepositoryBase<SalesOrderStatus, int>> _soStatusRepo = new();
    private readonly Mock<IPartyDebtRepository> _partyDebtRepo = new();
    private readonly Mock<IDebtTransactionRepository> _debtTxRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<IPaddyLotRepository> _paddyLotRepo = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Mock<Backend.Application.BackgroundJobs.DebtDueOverdue.IDebtAgingCalculationService> _aging = new();
    private readonly Mock<IRepositoryBase<PaddyLotBag, int>> _bagRepo = new();
    private readonly Mock<IRepositoryBase<PaddyLotBagContent, int>> _bagContentRepo = new();
    private readonly Mock<IRepositoryBase<PaddyLotBagMovement, int>> _bagMovementRepo = new();
    private readonly Mock<ILocationRepository> _locationRepo = new();
    private readonly Mock<IRepositoryBase<PaddyLotBagAllocation, int>> _bagAllocRepo = new();
    private readonly Mock<IQualityInspectionRepository> _qiRepo = new();
    private readonly Mock<IRepositoryBase<QualityInspectionBagResult, int>> _qiBagResultRepo = new();

    public OutboundOrderServiceTests()
    {
        _obRepo
            .Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(() => CreateDbTransactionMock().Object);

        _bagAllocRepo
            .Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBagAllocation, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<PaddyLotBagAllocation, object>>[]>()))
            .Returns(new List<PaddyLotBagAllocation>().AsQueryable().BuildMock());

        _bagAllocRepo
            .Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBagAllocation, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<PaddyLotBagAllocation>().AsQueryable().BuildMock());

        _bagMovementRepo
            .Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBagMovement, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<PaddyLotBagMovement>().AsQueryable().BuildMock());
        // Outbound dispatch sends a notification after the DB transaction commits.
        // Mock the async side effect explicitly so service tests never depend on
        // Moq's default Task return value.
        _dispatcher
            .Setup(x => x.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<NotificationTarget>(),
                It.IsAny<object[]?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>()))
            .Returns(Task.CompletedTask);
    }

    private OutboundOrderService Sut(bool withLocationRepository = false) => new(
        outboundOrderRepository: _obRepo.Object,
        outboundStatusRepository: _obStatusRepo.Object,
        allocationRepository: _allocRepo.Object,
        inventoryRepository: _invRepo.Object,
        inventoryTransactionRepository: _invTxRepo.Object,
        salesOrderRepository: _soRepo.Object,
        salesOrderStatusRepository: _soStatusRepo.Object,
        partyDebtRepository: _partyDebtRepo.Object,
        debtTransactionRepository: _debtTxRepo.Object,
        httpContextAccessor: _http.Object,
        paddyLotRepository: _paddyLotRepo.Object,
        notificationDispatcher: _dispatcher.Object,
        scheduledJobService: null,
        debtAgingService: _aging.Object,
        bagRepository: _bagRepo.Object,
        bagContentRepository: _bagContentRepo.Object,
        bagMovementRepository: _bagMovementRepo.Object,
        locationRepository: withLocationRepository ? _locationRepo.Object : null,
        bagAllocationRepository: _bagAllocRepo.Object,
        qualityInspectionRepository: _qiRepo.Object,
        qualityInspectionBagResultRepository: _qiBagResultRepo.Object);

    [Fact]
    public async Task GetAllocationCandidatesAsync_ExcludesStagingAndLockedLocations()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            SalesOrderId = 3,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Draft },
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new() { Id = 10, ProductVariantId = 100 }
            }
        };
        var lot = CreateLot(5);
        var inventories = new List<Backend.Domain.Entities.Inventory>
        {
            CreateCandidateInventory(1, lot, new Location { Id = 11, IsActive = true, SlotCode = "A-01" }),
            CreateCandidateInventory(2, lot, new Location { Id = 12, IsActive = true, SlotCode = "STAGING", IsOutboundStaging = true }),
            CreateCandidateInventory(3, lot, new Location { Id = 13, IsActive = true, SlotCode = "A-03", OutboundLockOrderId = 99 })
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _invRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, object>>[]>()!))
            .Returns((Expression<Func<Backend.Domain.Entities.Inventory, bool>> predicate, bool _,
                    Expression<Func<Backend.Domain.Entities.Inventory, object>>[] __) =>
                inventories.AsQueryable().Where(predicate.Compile()).AsQueryable().BuildMock());
        _invTxRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<InventoryTransaction, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<InventoryTransaction>().AsQueryable().BuildMock());
        _bagRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBag, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<PaddyLotBag>().AsQueryable().BuildMock());

        var result = await Sut().GetAllocationCandidatesAsync(1);

        result.Status.Should().Be(200);
        var candidates = result.Resources.Should()
            .BeAssignableTo<IEnumerable<OutboundAllocationCandidateDto>>()
            .Subject.ToList();
        candidates.Should().ContainSingle();
        candidates[0].InventoryId.Should().Be(1);
    }

    [Fact]
    public async Task GetAllocationCandidatesAsync_PickingAllowsLocationLockedBySameOutbound()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            SalesOrderId = 3,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new() { Id = 10, ProductVariantId = 100, QuantityOrdered = 10 }
            }
        };
        var lot = CreateLot(5);
        var inventories = new List<Backend.Domain.Entities.Inventory>
        {
            CreateCandidateInventory(1, lot, new Location
            {
                Id = 11,
                IsActive = true,
                SlotCode = "A-01",
                OutboundLockOrderId = 1
            })
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _invRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, object>>[]>()!))
            .Returns((Expression<Func<Backend.Domain.Entities.Inventory, bool>> predicate, bool _,
                    Expression<Func<Backend.Domain.Entities.Inventory, object>>[] __) =>
                inventories.AsQueryable().Where(predicate.Compile()).AsQueryable().BuildMock());
        _invTxRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<InventoryTransaction, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<InventoryTransaction>().AsQueryable().BuildMock());
        _bagRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBag, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<PaddyLotBag>().AsQueryable().BuildMock());

        var result = await Sut().GetAllocationCandidatesAsync(1);

        result.Status.Should().Be(200);
        var candidates = result.Resources.Should()
            .BeAssignableTo<IEnumerable<OutboundAllocationCandidateDto>>()
            .Subject.ToList();
        candidates.Should().ContainSingle();
        candidates[0].InventoryId.Should().Be(1);
    }

    [Fact]
    public async Task GetAllocationCandidatesAsync_QcReplacementExcludesCurrentActiveBagWeight()
    {
        var lot = CreateLot(5);
        var location = new Location
        {
            Id = 11,
            IsActive = true,
            SlotCode = "A-01",
            OutboundLockOrderId = 1
        };
        var activeItemAllocation = new OutboundOrderItemAllocation
        {
            Id = 31,
            QuantityAllocated = 50
        };
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            SalesOrderId = 3,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new()
                {
                    Id = 10,
                    ProductVariantId = 100,
                    QuantityOrdered = 100,
                    Allocations = new List<OutboundOrderItemAllocation> { activeItemAllocation }
                }
            }
        };
        var inventory = CreateCandidateInventory(1, lot, location);
        inventory.QuantityReserved = 50;
        var activeBag = CreateBag(1, 101, lot, location.Id, stackOrder: 2, weightKg: 50);
        activeBag.Allocations.Add(new PaddyLotBagAllocation
        {
            BagId = activeBag.Id,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = order.Id,
            Status = PaddyLotBagAllocationStatuses.Active
        });
        var replacementBag = CreateBag(2, 102, lot, location.Id, stackOrder: 1, weightKg: 50);

        _obRepo.Setup(r => r.GetByIdDetailAsync(order.Id)).ReturnsAsync(order);
        _invRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, object>>[]>()!))
            .Returns(new List<Backend.Domain.Entities.Inventory> { inventory }.AsQueryable().BuildMock());
        _invTxRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<InventoryTransaction, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<InventoryTransaction>
            {
                new()
                {
                    InventoryId = inventory.Id,
                    ReferenceType = InventoryReferenceTypeConstants.SalesOrder,
                    ReferenceId = order.SalesOrderId,
                    TransactionType = InventoryTransactionTypeConstants.Reserve,
                    Quantity = 50
                }
            }.AsQueryable().BuildMock());
        _bagRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBag, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<PaddyLotBag> { activeBag, replacementBag }.AsQueryable().BuildMock());

        var result = await Sut().GetAllocationCandidatesAsync(order.Id);

        result.Status.Should().Be(200);
        var candidates = result.Resources.Should()
            .BeAssignableTo<IEnumerable<OutboundAllocationCandidateDto>>()
            .Subject.ToList();
        candidates.Should().ContainSingle();
        var candidate = candidates[0];
        candidate.SelectableQuantity.Should().Be(50);
        candidate.FullBagCount.Should().Be(1);
    }

    [Fact]
    public async Task AllocateAsync_QcReplacementCannotExceedCurrentShortfall()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new()
                {
                    Id = 10,
                    ProductVariantId = 100,
                    QuantityOrdered = 150,
                    Allocations = new List<OutboundOrderItemAllocation>
                    {
                        new() { QuantityAllocated = 100 }
                    }
                }
            }
        };
        _obRepo.Setup(r => r.GetByIdDetailAsync(order.Id)).ReturnsAsync(order);

        var result = await Sut().AllocateAsync(order.Id, new AllocateOutboundDto
        {
            Allocations = new List<AllocateItemDto>
            {
                new()
                {
                    OutboundOrderItemId = 10,
                    Lots = new List<AllocateItemLotDto>
                    {
                        new() { InventoryId = 1, QuantityAllocated = 60 }
                    }
                }
            }
        });

        result.Status.Should().Be(422);
        result.Message.Should().Contain("50");
    }

    [Fact]
    public async Task ForceUnlockAsync_RejectsReasonLongerThan500Characters()
    {
        var result = await Sut(withLocationRepository: true)
            .ForceUnlockAsync(1, new string('a', 501));

        result.Status.Should().Be(422);
        _obRepo.Verify(r => r.GetByIdDetailAsync(It.IsAny<int>()), Times.Never);
        _locationRepo.Verify(r => r.ReleaseOutboundLocksAsync(
            It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>?>()), Times.Never);
    }

    [Fact]
    public async Task ForceUnlockAsync_PreservesOriginalOrderNote_WhenAuditEntryMustBeTruncated()
    {
        var order = new OutboundOrder { Id = 1, Note = new string('x', 990) };
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _locationRepo.Setup(r => r.ReleaseOutboundLocksAsync(
                1, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .ReturnsAsync(1);

        var result = await Sut(withLocationRepository: true)
            .ForceUnlockAsync(1, "Kiểm tra an toàn");

        result.Status.Should().Be(200);
        order.Note.Should().NotBeNull();
        order.Note!.Length.Should().Be(OutboundOrderConstants.NoteMaxLength);
        order.Note.Should().StartWith(new string('x', 990));
        order.Note![990].Should().Be('\n');
        _obRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ForceUnlockAsync_AppendsCompleteAuditEntry_WhenOrderNoteHasRoom()
    {
        var order = new OutboundOrder { Id = 1, Note = "Giao tại cửa sau" };
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _locationRepo.Setup(r => r.ReleaseOutboundLocksAsync(
                1, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .ReturnsAsync(1);

        var result = await Sut(withLocationRepository: true)
            .ForceUnlockAsync(1, "Kiểm tra an toàn");

        result.Status.Should().Be(200);
        order.Note.Should().Be("Giao tại cửa sau\nMở khóa cột thủ công: Kiểm tra an toàn");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync((OutboundOrder?)null);

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task GetByIdAsync_GroupsSameWeightAllocations_ForCompactBagUi()
    {
        var lot = new PaddyLotEntity { Id = 5, LotCode = "LOT-RICE-001" };
        var location = new Location { Id = 7, SlotCode = "A-01" };
        var order = new OutboundOrder
        {
            Id = 1,
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new()
                {
                    Id = 20,
                    Allocations = new List<OutboundOrderItemAllocation>
                    {
                        new() { Id = 101, InventoryId = 9, PaddyLotId = 5, PaddyLot = lot, LocationId = 7, Location = location, QuantityAllocated = 50, QuantityPicked = 50 },
                        new() { Id = 102, InventoryId = 9, PaddyLotId = 5, PaddyLot = lot, LocationId = 7, Location = location, QuantityAllocated = 50, QuantityPicked = 25 },
                        new() { Id = 103, InventoryId = 9, PaddyLotId = 5, PaddyLot = lot, LocationId = 7, Location = location, QuantityAllocated = 25, QuantityPicked = 0 }
                    }
                }
            }
        };
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);

        var result = await Sut().GetByIdAsync(1);

        var dto = result.Resources.Should()
            .BeOfType<Backend.Application.DTOs.OutboundOrders.OutboundOrderDetailDto>()
            .Subject;
        var groups = dto.Items.Single().AllocationGroups;
        groups.Should().HaveCount(2);
        groups[0].BagCount.Should().Be(2);
        groups[0].WeightPerBagKg.Should().Be(50);
        groups[0].TotalAllocatedKg.Should().Be(100);
        groups[0].TotalPickedKg.Should().Be(75);
        groups[0].AllocationIds.Should().Equal(101, 102);
        groups[1].BagCount.Should().Be(1);
        groups[1].WeightPerBagKg.Should().Be(25);
    }

    [Fact]
    public async Task CancelAsync_NonCancellableState_ReturnsConflict()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Name = "Đang giao hàng", Code = OutboundOrderStatusNames.Dispatched }
        };
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);

        // Có lý do hợp lệ nhưng trạng thái không cho hủy → vẫn phải 409.
        var result = await Sut().CancelAsync(1, "Khách đổi lịch giao");

        result.Status.Should().Be(409);
        order.CancelReason.Should().BeNull();
    }

    [Fact]
    public async Task CancelAsync_DraftOrder_SavesCancelReason()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Id = 1, Name = "Nháp", Code = OutboundOrderStatusNames.Draft },
            OutboundOrderItems = new List<OutboundOrderItem>()
        };
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<OutboundOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<OutboundOrderStatus, object>>[]>()))
            .ReturnsAsync(new OutboundOrderStatus { Id = 6, Name = "Đã hủy", Code = OutboundOrderStatusNames.Cancelled });

        var result = await Sut().CancelAsync(1, "  Khách đổi lịch giao  ");

        result.Status.Should().Be(200);
        order.OutboundOrderStatusId.Should().Be(6);
        // Lý do được trim trước khi lưu.
        order.CancelReason.Should().Be("Khách đổi lịch giao");
        _obRepo.Verify(r => r.UpdateAsync(order), Times.Once);
        _obRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ReasonLongerThan500Chars_IsTruncated()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Id = 1, Name = "Nháp", Code = OutboundOrderStatusNames.Draft },
            OutboundOrderItems = new List<OutboundOrderItem>()
        };
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<OutboundOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<OutboundOrderStatus, object>>[]>()))
            .ReturnsAsync(new OutboundOrderStatus { Id = 6, Name = "Đã hủy", Code = OutboundOrderStatusNames.Cancelled });

        var result = await Sut().CancelAsync(1, new string('a', 600));

        result.Status.Should().Be(200);
        // Cột CancelReason chỉ chứa được 500 ký tự nên service tự cắt bớt.
        order.CancelReason.Should().HaveLength(500);
    }

    [Fact]
    public async Task ConfirmDispatchAsync_MissingDueDate_ReturnsUnprocessableEntity()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(CreatePackedOrderWithReceivable());

        var result = await Sut().ConfirmDispatchAsync(1, new Backend.Application.DTOs.OutboundOrders.ConfirmDispatchDto());

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task ConfirmDispatchAsync_PastDueDate_ReturnsUnprocessableEntity()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(CreatePackedOrderWithReceivable());

        var result = await Sut().ConfirmDispatchAsync(1, new Backend.Application.DTOs.OutboundOrders.ConfirmDispatchDto
        {
            DueDate = DateTime.Today.AddDays(-1)
        });

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task ConfirmPackingAsync_PartiallyPickedOrder_ReturnsUnprocessableEntity()
    {
        var order = CreatePackedOrderWithReceivable();
        order.OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking };
        order.OutboundOrderItems.Single().QuantityOrdered = 10;
        order.OutboundOrderItems.Single().Allocations.Single().QuantityPicked = 5;
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);

        var result = await Sut().ConfirmPackingAsync(1, new ConfirmPackingDto { QrCode = "OUT-001" });

        result.Status.Should().Be(422);
        _obRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfirmPackingAsync_LeavesBagAndInventoryAtSource_AndKeepsLock()
    {
        var lot = CreateLot(5);
        var sourceLocation = new Location
        {
            Id = 7,
            WarehouseId = 2,
            ZoneName = "A",
            SlotCode = "A-01",
            IsActive = true,
            CurrentOccupancy = 50,
            CurrentProductVariantId = 100,
            OutboundLockOrderId = 1
        };
        var sourceInventory = CreateCandidateInventory(9, lot, sourceLocation);
        sourceInventory.QuantityOnHand = 50;
        sourceInventory.QuantityReserved = 50;
        sourceInventory.CostPrice = 12;
        var allocation = new OutboundOrderItemAllocation
        {
            Id = 101,
            InventoryId = sourceInventory.Id,
            Inventory = sourceInventory,
            PaddyLotId = lot.Id,
            PaddyLot = lot,
            LocationId = sourceLocation.Id,
            Location = sourceLocation,
            QuantityAllocated = 50,
            QuantityPicked = 50
        };
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new()
                {
                    Id = 20,
                    ProductVariantId = 100,
                    QuantityOrdered = 50,
                    QuantityPicked = 50,
                    Allocations = new List<OutboundOrderItemAllocation> { allocation }
                }
            }
        };
        var bag = CreateBag(1, 11, lot, sourceLocation.Id, stackOrder: 1, weightKg: 50);
        bag.Location = sourceLocation;
        var bags = new List<PaddyLotBag> { bag };
        _bagRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBag, bool>>>(),
                It.IsAny<bool>()))
            .Returns((Expression<Func<PaddyLotBag, bool>> predicate, bool _) =>
                bags.AsQueryable().Where(predicate.Compile()).AsQueryable().BuildMock());

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        var dbTransaction = CreateDbTransactionMock();
        _obRepo.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(dbTransaction.Object);
        _locationRepo.Setup(r => r.ReleaseOutboundLocksAsync(
                order.Id, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .ReturnsAsync(1);
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<OutboundOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<OutboundOrderStatus, object>>[]>()!))
            .ReturnsAsync(new OutboundOrderStatus { Id = 3, Code = OutboundOrderStatusNames.Packed });

        var totalBefore = sourceInventory.QuantityOnHand;
        var result = await Sut(withLocationRepository: true)
            .ConfirmPackingAsync(1, new ConfirmPackingDto { QrCode = "OUT-001" });

        result.Status.Should().Be(200);
        bag.LocationId.Should().Be(sourceLocation.Id);
        bag.Status.Should().Be(PaddyLotBagStatuses.Stored);
        sourceInventory.QuantityOnHand.Should().Be(totalBefore);
        sourceInventory.QuantityReserved.Should().Be(50);
        sourceLocation.CurrentOccupancy.Should().Be(50);
        _locationRepo.Verify(r => r.ReleaseOutboundLocksAsync(
            It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>?>()), Times.Never);
        dbTransaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmDispatchAsync_PartiallyPickedOrder_ReturnsUnprocessableEntity()
    {
        var order = CreatePackedOrderWithReceivable();
        order.OutboundOrderItems.Single().QuantityOrdered = 10;
        order.OutboundOrderItems.Single().Allocations.Single().QuantityPicked = 5;
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);

        var result = await Sut().ConfirmDispatchAsync(1, new ConfirmDispatchDto());

        result.Status.Should().Be(422);
        _obRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfirmDispatchAsync_DecreasesLocationOccupancy()
    {
        // Arrange
        var location = new Location { Id = 10, CurrentOccupancy = 50, SlotCode = "LOC-10" };
        var inventory = new Backend.Domain.Entities.Inventory { Id = 1, QuantityOnHand = 50, QuantityReserved = 10 };
        var allocation = new OutboundOrderItemAllocation
        {
            Id = 1,
            InventoryId = 1,
            LocationId = 10,
            Location = location,
            Inventory = inventory,
            QuantityAllocated = 10,
            QuantityPicked = 10,
            UnitCostPrice = 5
        };

        var item = new OutboundOrderItem
        {
            Id = 1,
            QuantityOrdered = 10,
            QuantityPicked = 10,
            Allocations = new List<OutboundOrderItemAllocation> { allocation }
        };

        var order = new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Name = "Đã đóng gói", Code = OutboundOrderStatusNames.Packed },
            OutboundOrderItems = new List<OutboundOrderItem> { item }
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        var dbTransaction = CreateDbTransactionMock();
        _obRepo.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(dbTransaction.Object);
        
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<System.Func<OutboundOrderStatus, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<System.Linq.Expressions.Expression<System.Func<OutboundOrderStatus, object>>[]>()))
            .ReturnsAsync(new OutboundOrderStatus { Id = 3, Name = "Đang giao hàng", Code = OutboundOrderStatusNames.Dispatched });

        // Act
        var result = await Sut().ConfirmDispatchAsync(1, new Backend.Application.DTOs.OutboundOrders.ConfirmDispatchDto());

        // Assert
        result.Status.Should().Be(200);
        location.CurrentOccupancy.Should().Be(40); // 50 - 10 = 40
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_AllocatesSelectedLotFromTopBag()
    {
        var lot = CreateLot(5);
        var inventory = CreateInventory(id: 9, lotId: lot.Id, locationId: 7);
        var bag = CreateBag(id: 1, bagNo: 11, lot, locationId: 7, stackOrder: 3, weightKg: 50);
        SetupBagAllocationSources(new[] { inventory }, new[] { bag });

        var result = await InvokeBuildRequestedBagAllocationsAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 20 } });

        result.Should().ContainSingle();
        result[0].InventoryId.Should().Be(inventory.Id);
        result[0].QuantityAllocated.Should().Be(20);
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_CurrentOutboundBagIsTransparentButNotSelected()
    {
        var lot = CreateLot(5);
        var inventory = CreateInventory(id: 9, lotId: lot.Id, locationId: 7);
        var currentBag = CreateBag(id: 1, bagNo: 11, lot, locationId: 7, stackOrder: 2, weightKg: 50);
        currentBag.Allocations.Add(new PaddyLotBagAllocation
        {
            BagId = currentBag.Id,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 1,
            Status = PaddyLotBagAllocationStatuses.Active
        });
        var replacementBag = CreateBag(id: 2, bagNo: 12, lot, locationId: 7, stackOrder: 1, weightKg: 50);
        SetupBagAllocationSources(new[] { inventory }, new[] { currentBag, replacementBag });

        var result = await InvokeBuildRequestedBagPlansAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 20 } });

        result.Should().ContainSingle();
        result[0].BagId.Should().Be(replacementBag.Id);
        result[0].QuantityAllocated.Should().Be(20);
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_ExplicitFullBagDoesNotDuplicateCurrentOutboundBag()
    {
        var lot = CreateLot(5);
        var inventory = CreateInventory(id: 9, lotId: lot.Id, locationId: 7);
        var currentBag = CreateBag(id: 1, bagNo: 11, lot, locationId: 7, stackOrder: 2, weightKg: 50);
        currentBag.Allocations.Add(new PaddyLotBagAllocation
        {
            BagId = currentBag.Id,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 1,
            Status = PaddyLotBagAllocationStatuses.Active
        });
        var replacementBag = CreateBag(id: 2, bagNo: 12, lot, locationId: 7, stackOrder: 1, weightKg: 50);
        SetupBagAllocationSources(new[] { inventory }, new[] { currentBag, replacementBag });

        var result = await InvokeBuildRequestedBagPlansAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 50 } });

        result.Should().ContainSingle();
        result[0].BagId.Should().Be(replacementBag.Id);
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_OtherOutboundBagRemainsLifoBlocker()
    {
        var lot = CreateLot(5);
        var inventory = CreateInventory(id: 9, lotId: lot.Id, locationId: 7);
        var blockingBag = CreateBag(id: 1, bagNo: 11, lot, locationId: 7, stackOrder: 2, weightKg: 50);
        blockingBag.Allocations.Add(new PaddyLotBagAllocation
        {
            BagId = blockingBag.Id,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 99,
            Status = PaddyLotBagAllocationStatuses.Active
        });
        var replacementBag = CreateBag(id: 2, bagNo: 12, lot, locationId: 7, stackOrder: 1, weightKg: 50);
        SetupBagAllocationSources(new[] { inventory }, new[] { blockingBag, replacementBag });

        var act = () => InvokeBuildRequestedBagPlansAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 20 } });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*bao #11*");
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_CurrentOutboundOpenBagIsNotAllocatedAgain()
    {
        var lot = CreateLot(5);
        var inventory = CreateInventory(id: 9, lotId: lot.Id, locationId: 7);
        var openBag = CreateBag(id: 1, bagNo: 11, lot, locationId: 7, stackOrder: 0, weightKg: 10);
        openBag.BagKind = PaddyLotBagKinds.Finished;
        openBag.Allocations.Add(new PaddyLotBagAllocation
        {
            BagId = openBag.Id,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 1,
            Status = PaddyLotBagAllocationStatuses.Active
        });
        var replacementBag = CreateBag(id: 2, bagNo: 12, lot, locationId: 7, stackOrder: 1, weightKg: 50);
        SetupBagAllocationSources(new[] { inventory }, new[] { openBag, replacementBag });

        var result = await InvokeBuildRequestedBagPlansAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 10 } });

        result.Should().ContainSingle();
        result[0].BagId.Should().Be(replacementBag.Id);
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_ExplicitFullBag_DoesNotDrainOpenBagFirst()
    {
        var lot = CreateLot(5);
        var inventory = CreateInventory(id: 9, lotId: lot.Id, locationId: 7);
        var fullBag = CreateBag(id: 1, bagNo: 11, lot, locationId: 7, stackOrder: 1, weightKg: 50);
        var openBag = CreateBag(id: 2, bagNo: 12, lot, locationId: 7, stackOrder: 2, weightKg: 10);
        SetupBagAllocationSources(new[] { inventory }, new[] { openBag, fullBag });

        var result = await InvokeBuildRequestedBagAllocationsAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 50 } });

        result.Should().ContainSingle();
        result[0].InventoryId.Should().Be(inventory.Id);
        result[0].QuantityAllocated.Should().Be(50);
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_SeparatePartialLines_KeepOpenAndSplitPlan()
    {
        var lot = CreateLot(5);
        var inventory = CreateInventory(id: 9, lotId: lot.Id, locationId: 7);
        var fullBag = CreateBag(id: 1, bagNo: 11, lot, locationId: 7, stackOrder: 1, weightKg: 50);
        var openBag = CreateBag(id: 2, bagNo: 12, lot, locationId: 7, stackOrder: 2, weightKg: 10);
        SetupBagAllocationSources(new[] { inventory }, new[] { openBag, fullBag });

        var result = await InvokeBuildRequestedBagAllocationsAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[]
            {
                new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 10 },
                new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 40 }
            });

        result.Select(x => x.QuantityAllocated).Should().Equal(10, 40);
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_InvalidInventoryShape_ThrowsFriendlyError()
    {
        var inventory = CreateInventory(id: 9, lotId: 5, locationId: null);
        SetupBagAllocationSources(new[] { inventory }, Array.Empty<PaddyLotBag>());

        var act = () => InvokeBuildRequestedBagAllocationsAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 20 } });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*không khớp sản phẩm hoặc kho xuất*");
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_OtherLotAtTop_ThrowsBlockerError()
    {
        var selectedLot = CreateLot(5);
        var blockingLot = CreateLot(6);
        var inventory = CreateInventory(id: 9, lotId: selectedLot.Id, locationId: 7);
        var lowerSelectedBag = CreateBag(id: 1, bagNo: 11, selectedLot, locationId: 7, stackOrder: 1, weightKg: 50);
        var topBlockingBag = CreateBag(id: 2, bagNo: 12, blockingLot, locationId: 7, stackOrder: 2, weightKg: 50);
        SetupBagAllocationSources(new[] { inventory }, new[] { lowerSelectedBag, topBlockingBag });

        var act = () => InvokeBuildRequestedBagAllocationsAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[] { new AllocateItemLotDto { InventoryId = inventory.Id, QuantityAllocated = 20 } });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*bao #12 của lô khác đang chặn phía trên*");
    }

    [Fact]
    public async Task BuildRequestedBagAllocationsAsync_SelectedLocationWithoutBag_ThrowsMissingDemand()
    {
        var firstLot = CreateLot(5);
        var secondLot = CreateLot(6);
        var firstInventory = CreateInventory(id: 9, lotId: firstLot.Id, locationId: 7);
        var secondInventory = CreateInventory(id: 10, lotId: secondLot.Id, locationId: 8);
        var firstBag = CreateBag(id: 1, bagNo: 11, firstLot, locationId: 7, stackOrder: 1, weightKg: 50);
        SetupBagAllocationSources(new[] { firstInventory, secondInventory }, new[] { firstBag });

        var act = () => InvokeBuildRequestedBagAllocationsAsync(
            productVariantId: 100,
            warehouseId: 2,
            new[]
            {
                new AllocateItemLotDto { InventoryId = firstInventory.Id, QuantityAllocated = 20 },
                new AllocateItemLotDto { InventoryId = secondInventory.Id, QuantityAllocated = 20 }
            });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Không thể lấy đủ 20 kg*");
    }

    [Fact]
    public async Task CompleteDeliveryAsync_NegativePayment_ReturnsUnprocessableEntity()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Dispatched }
        });

        var result = await Sut().CompleteDeliveryAsync(1, new Backend.Application.DTOs.OutboundOrders.CompleteDeliveryDto
        {
            ReceiverName = "Nguyễn Văn A",
            PaymentAmount = -1
        });

        result.Status.Should().Be(422);
        _obRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_PartialPayment_UpdatesDebtAndCreatesPayment()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            SalesOrderId = 10,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Dispatched },
            OutboundOrderItems = new List<OutboundOrderItem>()
        };
        var debt = new Backend.Domain.Entities.PartyDebt { Id = 20, CurrentBalance = 100, IsActive = true };
        var charge = new DebtTransaction
        {
            Id = 30,
            PartyDebtId = 20,
            TransactionType = LookupCodes.DebtTransactionType.Charge,
            Amount = 100,
            RefType = "OUTBOUND_ORDER",
            RefId = 1
        };
        var dbTransaction = CreateDbTransactionMock();

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _obRepo.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(dbTransaction.Object);
        _debtTxRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<DebtTransaction, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<DebtTransaction, object>>[]>()!))
            .ReturnsAsync(charge);
        _partyDebtRepo.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(debt);
        _debtTxRepo.Setup(r => r.FindByCondition(
                It.IsAny<System.Linq.Expressions.Expression<Func<DebtTransaction, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<DebtTransaction> { charge }.AsQueryable().BuildMock());
        _aging.Setup(x => x.CalculateDebtDocuments(debt, It.IsAny<List<DebtTransaction>>()))
            .Returns(new List<Backend.Application.BackgroundJobs.DebtDueOverdue.DebtDocumentAllocation>
            {
                new() { RefType = "OUTBOUND_ORDER", RefId = 1, OutstandingAmount = 100 }
            });
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrderStatus, object>>[]>()!))
            .ReturnsAsync(new OutboundOrderStatus { Id = 4, Code = OutboundOrderStatusNames.Completed });
        _obRepo.Setup(r => r.FindByCondition(
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrder, object>>[]>()!))
            .Returns(new List<OutboundOrder> { order }.AsQueryable().BuildMock());
        _soRepo.Setup(r => r.GetByIdDetailAsync(10)).ReturnsAsync(new SalesOrder
        {
            Id = 10,
            SalesOrderItems = new List<SalesOrderItem>()
        });
        _soStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SalesOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<SalesOrderStatus, object>>[]>()!))
            .ReturnsAsync(new SalesOrderStatus { Id = 5, Code = SalesOrderStatusNames.Completed });

        var result = await Sut().CompleteDeliveryAsync(1, new Backend.Application.DTOs.OutboundOrders.CompleteDeliveryDto
        {
            ReceiverName = "Nguyễn Văn A",
            PaymentAmount = 40
        });

        result.Status.Should().Be(200);
        debt.CurrentBalance.Should().Be(60);
        _debtTxRepo.Verify(r => r.CreateAsync(It.Is<DebtTransaction>(x =>
            x.TransactionType == LookupCodes.DebtTransactionType.Payment &&
            x.Amount == 40 && x.RefType == "OUTBOUND_ORDER" && x.RefId == 1)), Times.Once);
        dbTransaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> CreateDbTransactionMock()
    {
        var transaction = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
        transaction
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transaction
            .Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return transaction;
    }

    private static OutboundOrder CreatePackedOrderWithReceivable()
    {
        return new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Packed },
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new()
                {
                    QuantityOrdered = 1,
                    SalesOrderItem = new SalesOrderItem
                    {
                        QuantityOrdered = 1,
                        LineAmount = 100
                    },
                    Allocations = new List<OutboundOrderItemAllocation>
                    {
                        new() { QuantityPicked = 1 }
                    }
                }
            }
        };
    }

    private void SetupBagAllocationSources(IEnumerable<Backend.Domain.Entities.Inventory> inventories, IEnumerable<PaddyLotBag> bags)
    {
        _invRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, object>>[]>()))
            .Returns(inventories.AsQueryable().BuildMock());

        _bagRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBag, bool>>>(),
                It.IsAny<bool>()))
            .Returns(bags.AsQueryable().BuildMock());
    }

    private async Task<List<AllocateItemLotDto>> InvokeBuildRequestedBagAllocationsAsync(
        int productVariantId,
        int warehouseId,
        IReadOnlyCollection<AllocateItemLotDto> requestedLots)
    {
        var method = typeof(OutboundOrderService).GetMethod(
            "BuildRequestedBagAllocationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        try
        {
            var taskObj = method!.Invoke(
                Sut(),
                new object[] { 1, productVariantId, warehouseId, requestedLots });

            if (taskObj is Task task)
            {
                await task.ConfigureAwait(false);
                var resultProperty = taskObj.GetType().GetProperty("Result");
                var result = (System.Collections.IEnumerable)resultProperty!.GetValue(taskObj)!;
                var list = new List<AllocateItemLotDto>();
                foreach (var item in result)
                {
                    var itemType = item.GetType();
                    var invId = (int)itemType.GetProperty("InventoryId")!.GetValue(item)!;
                    var qty = (decimal)itemType.GetProperty("QuantityAllocated")!.GetValue(item)!;
                    list.Add(new AllocateItemLotDto { InventoryId = invId, QuantityAllocated = qty });
                }
                return list;
            }

            return new List<AllocateItemLotDto>();
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static Backend.Domain.Entities.Inventory CreateInventory(int id, int? lotId, int? locationId)
        => new()
        {
            Id = id,
            ProductVariantId = 100,
            WarehouseId = 2,
            PaddyLotId = lotId,
            LocationId = locationId,
            QuantityOnHand = 100
        };

    private static Backend.Domain.Entities.Inventory CreateCandidateInventory(
        int id, PaddyLotEntity lot, Location location)
        => new()
        {
            Id = id,
            ProductVariantId = 100,
            WarehouseId = 2,
            PaddyLotId = lot.Id,
            PaddyLot = lot,
            LocationId = location.Id,
            Location = location,
            QuantityOnHand = 100,
            QuantityReserved = 0
        };

    private static PaddyLotEntity CreateLot(int id)
        => new()
        {
            Id = id,
            LotCode = $"LOT-{id}",
            LotType = "RICE",
            ProductVariantId = 100,
            WarehouseId = 2,
            Status = new LotStatus { Id = 1, Code = "AVAILABLE", Name = "Available", Color = "#00AA00", IsSellable = true }
        };

    private static PaddyLotBag CreateBag(int id, int bagNo, PaddyLotEntity lot, int locationId, int stackOrder, decimal weightKg)
    {
        var bag = new PaddyLotBag
        {
            Id = id,
            BagNo = bagNo,
            LotId = lot.Id,
            Lot = lot,
            LocationId = locationId,
            StackOrder = stackOrder,
            WeightKg = weightKg,
            StandardWeightKg = 50,
            IsFull = weightKg >= 50,
            Status = PaddyLotBagStatuses.Stored
        };
        bag.Contents.Add(new PaddyLotBagContent
        {
            Id = id,
            BagId = id,
            Bag = bag,
            LotId = lot.Id,
            Lot = lot,
            WeightKg = weightKg
        });
        return bag;
    }

    [Fact]
    public async Task PickAsync_ValidPhysicalBags_UpdatesPickedWeightAndItemTotal()
    {
        var lot = CreateLot(5);
        var bag = CreateBag(1, 101, lot, 7, 1, 50);
        var itemAlloc = new OutboundOrderItemAllocation
        {
            Id = 11,
            OutboundOrderItemId = 1,
            InventoryId = 100,
            PaddyLotId = lot.Id,
            QuantityAllocated = 50,
            QuantityPicked = 0
        };
        var orderItem = new OutboundOrderItem
        {
            Id = 1,
            ProductVariantId = 100,
            QuantityOrdered = 50,
            QuantityPicked = 0,
            Allocations = new List<OutboundOrderItemAllocation> { itemAlloc }
        };
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem> { orderItem }
        };

        var bagAlloc = new PaddyLotBagAllocation
        {
            Id = 21,
            BagId = bag.Id,
            Bag = bag,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 1,
            ReferenceItemId = itemAlloc.Id,
            AllocatedWeightKg = 50,
            PickedWeightKg = 0,
            Status = PaddyLotBagAllocationStatuses.Active
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _bagAllocRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBagAllocation, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<PaddyLotBagAllocation, object>>[]>()))
            .Returns(new List<PaddyLotBagAllocation> { bagAlloc }.AsQueryable().BuildMock());

        var result = await Sut().PickAsync(1, new Backend.Application.DTOs.OutboundOrders.PickOutboundDto
        {
            Picks = new List<Backend.Application.DTOs.OutboundOrders.PickPhysicalBagDto>
            {
                new() { BagAllocationId = 21, QuantityPicked = 50 }
            }
        });

        result.Status.Should().Be(200, result.Message);
        bagAlloc.PickedWeightKg.Should().Be(50);
        itemAlloc.QuantityPicked.Should().Be(50);
        orderItem.QuantityPicked.Should().Be(50);
    }

    [Fact]
    public async Task PickAsync_ExceedsAllocatedWeight_ReturnsUnprocessableEntity()
    {
        var lot = CreateLot(5);
        var bag = CreateBag(1, 101, lot, 7, 1, 50);
        var itemAlloc = new OutboundOrderItemAllocation
        {
            Id = 11,
            OutboundOrderItemId = 1,
            QuantityAllocated = 50,
            QuantityPicked = 0
        };
        var orderItem = new OutboundOrderItem
        {
            Id = 1,
            ProductVariantId = 100,
            QuantityOrdered = 50,
            QuantityPicked = 0,
            Allocations = new List<OutboundOrderItemAllocation> { itemAlloc }
        };
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem> { orderItem }
        };

        var bagAlloc = new PaddyLotBagAllocation
        {
            Id = 21,
            BagId = bag.Id,
            Bag = bag,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 1,
            ReferenceItemId = itemAlloc.Id,
            AllocatedWeightKg = 50,
            Status = PaddyLotBagAllocationStatuses.Active
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _bagAllocRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBagAllocation, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<PaddyLotBagAllocation, object>>[]>()))
            .Returns(new List<PaddyLotBagAllocation> { bagAlloc }.AsQueryable().BuildMock());

        var result = await Sut().PickAsync(1, new Backend.Application.DTOs.OutboundOrders.PickOutboundDto
        {
            Picks = new List<Backend.Application.DTOs.OutboundOrders.PickPhysicalBagDto>
            {
                new() { BagAllocationId = 21, QuantityPicked = 55 }
            }
        });

        result.Status.Should().Be(422);
    }

    private sealed record PhysicalBagPlanSnapshot(int BagId, int InventoryId, decimal QuantityAllocated);

    private async Task<List<PhysicalBagPlanSnapshot>> InvokeBuildRequestedBagPlansAsync(
        int productVariantId,
        int warehouseId,
        IReadOnlyCollection<AllocateItemLotDto> requestedLots)
    {
        var method = typeof(OutboundOrderService).GetMethod(
            "BuildRequestedBagAllocationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        try
        {
            var taskObj = method!.Invoke(
                Sut(),
                new object[] { 1, productVariantId, warehouseId, requestedLots });
            var task = (Task)taskObj!;
            await task.ConfigureAwait(false);

            var result = (System.Collections.IEnumerable)taskObj!.GetType()
                .GetProperty("Result")!.GetValue(taskObj)!;
            return result.Cast<object>().Select(item =>
            {
                var type = item.GetType();
                return new PhysicalBagPlanSnapshot(
                    (int)type.GetProperty("BagId")!.GetValue(item)!,
                    (int)type.GetProperty("InventoryId")!.GetValue(item)!,
                    (decimal)type.GetProperty("QuantityAllocated")!.GetValue(item)!);
            }).ToList();
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    [Fact]
    public async Task PickAsync_RejectsQuantityBelowAllocation_InsteadOfCreatingShortfall()
    {
        var lot = CreateLot(5);
        var bag = CreateBag(1, 101, lot, 7, 1, 50);
        var itemAlloc = new OutboundOrderItemAllocation
        {
            Id = 11,
            OutboundOrderItemId = 1,
            QuantityAllocated = 50,
            QuantityPicked = 0
        };
        var orderItem = new OutboundOrderItem
        {
            Id = 1,
            ProductVariantId = 100,
            QuantityOrdered = 50,
            QuantityPicked = 0,
            Allocations = new List<OutboundOrderItemAllocation> { itemAlloc }
        };
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem> { orderItem }
        };
        var bagAlloc = new PaddyLotBagAllocation
        {
            Id = 21,
            BagId = bag.Id,
            Bag = bag,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 1,
            ReferenceItemId = itemAlloc.Id,
            AllocatedWeightKg = 50,
            Status = PaddyLotBagAllocationStatuses.Active
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _bagAllocRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBagAllocation, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<PaddyLotBagAllocation, object>>[]>()))
            .Returns(new List<PaddyLotBagAllocation> { bagAlloc }.AsQueryable().BuildMock());

        var result = await Sut().PickAsync(1, new Backend.Application.DTOs.OutboundOrders.PickOutboundDto
        {
            Picks = new List<Backend.Application.DTOs.OutboundOrders.PickPhysicalBagDto>
            {
                new() { BagAllocationId = 21, QuantityPicked = 49 }
            }
        });

        result.Status.Should().Be(422);
        bagAlloc.PickedWeightKg.Should().Be(0);
        itemAlloc.QuantityPicked.Should().Be(0);
    }

    [Fact]
    public async Task ConfirmPackingAsync_NullQrCode_Succeeds()
    {
        var lot = CreateLot(5);
        var sourceLocation = new Location
        {
            Id = 7,
            WarehouseId = 2,
            ZoneName = "A",
            SlotCode = "A-01",
            IsActive = true,
            CurrentOccupancy = 50,
            CurrentProductVariantId = 100
        };
        var sourceInventory = CreateCandidateInventory(9, lot, sourceLocation);
        sourceInventory.QuantityOnHand = 50;
        sourceInventory.QuantityReserved = 50;
        var allocation = new OutboundOrderItemAllocation
        {
            Id = 10,
            InventoryId = sourceInventory.Id,
            Inventory = sourceInventory,
            PaddyLotId = lot.Id,
            PaddyLot = lot,
            LocationId = sourceLocation.Id,
            Location = sourceLocation,
            QuantityAllocated = 50,
            QuantityPicked = 50
        };
        var item = new OutboundOrderItem
        {
            Id = 1,
            ProductVariantId = 100,
            QuantityOrdered = 50,
            QuantityPicked = 50,
            Allocations = new List<OutboundOrderItemAllocation> { allocation }
        };
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking },
            OutboundOrderItems = new List<OutboundOrderItem> { item }
        };

        var bag = CreateBag(1, 11, lot, sourceLocation.Id, stackOrder: 1, weightKg: 50);
        bag.Location = sourceLocation;
        var bags = new List<PaddyLotBag> { bag };
        _bagRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBag, bool>>>(),
                It.IsAny<bool>()))
            .Returns((Expression<Func<PaddyLotBag, bool>> predicate, bool _) =>
                bags.AsQueryable().Where(predicate.Compile()).AsQueryable().BuildMock());

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _locationRepo.Setup(r => r.ReleaseOutboundLocksAsync(
                order.Id, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>?>()))
            .ReturnsAsync(1);
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<OutboundOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<OutboundOrderStatus, object>>[]>()))
            .ReturnsAsync(new OutboundOrderStatus { Id = 3, Code = OutboundOrderStatusNames.Packed });

        var result = await Sut(withLocationRepository: true).ConfirmPackingAsync(1, new Backend.Application.DTOs.OutboundOrders.ConfirmPackingDto
        {
            QrCode = null,
            ActualWeightKg = 50
        });

        result.Status.Should().Be(200);
        order.OutboundOrderStatusId.Should().Be(3);
    }

    [Fact]
    public async Task ReportQualityIssueAsync_ValidBag_ChangesStatusToQualityHoldAndCreatesInspection()
    {
        var lot = CreateLot(5);
        var bag = CreateBag(1, 101, lot, 7, 1, 50);
        var order = new OutboundOrder
        {
            Id = 1,
            WarehouseId = 2,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Picking }
        };

        var bagAlloc = new PaddyLotBagAllocation
        {
            Id = 21,
            BagId = bag.Id,
            Bag = bag,
            ReferenceType = PaddyLotBagAllocationReferenceTypes.OutboundOrder,
            ReferenceId = 1,
            Status = PaddyLotBagAllocationStatuses.Active
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _bagAllocRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyLotBagAllocation, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<PaddyLotBagAllocation, object>>[]>()))
            .Returns(new List<PaddyLotBagAllocation> { bagAlloc }.AsQueryable().BuildMock());

        var result = await Sut().ReportQualityIssueAsync(1, 21, "Bao bị ẩm mốc");

        result.Status.Should().Be(200);
        bag.Status.Should().Be(PaddyLotBagStatuses.QualityHold);
        _qiRepo.Verify(r => r.CreateAsync(It.Is<Backend.Domain.Entities.QualityInspection>(q =>
            q.InspectionType == InspectionTypeConstants.OutboundException &&
            q.PaddyLotId == lot.Id)), Times.Once);
        _qiBagResultRepo.Verify(r => r.CreateAsync(It.Is<QualityInspectionBagResult>(b =>
            b.BagId == bag.Id &&
            b.BagWeightSnapshotKg == bag.WeightKg &&
            b.QualityResult == BagQualityResultConstants.IssueDetected &&
            b.Disposition == null)), Times.Once);
    }
}

