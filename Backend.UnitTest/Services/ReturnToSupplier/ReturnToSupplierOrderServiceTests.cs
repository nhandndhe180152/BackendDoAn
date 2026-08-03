using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.ReturnToSuppliers;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Constants;
using FluentAssertions;
using Backend.UnitTest.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.ReturnToSupplier;

[Trait("Service", "ReturnToSupplierOrder")]
public class ReturnToSupplierOrderServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<ILogger<ReturnToSupplierOrderService>> _loggerMock = new();

    private readonly List<ReturnToSupplierOrder> _orders = new();
    private readonly List<ReturnToSupplierOrderStatus> _statuses = new();
    private readonly List<ReturnToSupplierOrderItem> _items = new();
    private readonly List<global::Backend.Domain.Entities.Supplier> _suppliers = new();
    private readonly List<global::Backend.Domain.Entities.Inventory> _inventories = new();
    private readonly List<InventoryTransaction> _inventoryTransactions = new();
    private readonly List<Location> _locations = new();
    private readonly List<global::Backend.Domain.Entities.PaddyLot> _paddyLots = new();
    private readonly List<global::Backend.Domain.Entities.PartyDebt> _partyDebts = new();
    private readonly List<DebtTransaction> _debtTransactions = new();
    private readonly List<global::Backend.Domain.Entities.Warehouse> _warehouses = new();
    private readonly List<ProductVariant> _productVariants = new();

    private readonly ReturnToSupplierOrderService _sut;

    public ReturnToSupplierOrderServiceTests()
    {
        _contextMock.Setup(c => c.ReturnToSupplierOrders).Returns(() => MockDbSet(_orders).Object);
        _contextMock.Setup(c => c.ReturnToSupplierOrderStatuses).Returns(() => MockDbSet(_statuses).Object);
        _contextMock.Setup(c => c.ReturnToSupplierOrderItems).Returns(() => MockDbSet(_items).Object);
        _contextMock.Setup(c => c.Suppliers).Returns(() => MockDbSet(_suppliers).Object);
        _contextMock.Setup(c => c.Inventories).Returns(() => MockDbSet(_inventories).Object);
        _contextMock.Setup(c => c.InventoryTransactions).Returns(() => MockDbSet(_inventoryTransactions).Object);
        _contextMock.Setup(c => c.Locations).Returns(() => MockDbSet(_locations).Object);
        _contextMock.Setup(c => c.PaddyLots).Returns(() => MockDbSet(_paddyLots).Object);
        _contextMock.Setup(c => c.PartyDebts).Returns(() => MockDbSet(_partyDebts).Object);
        _contextMock.Setup(c => c.DebtTransactions).Returns(() => MockDbSet(_debtTransactions).Object);
        _contextMock.Setup(c => c.Warehouses).Returns(() => MockDbSet(_warehouses).Object);
        _contextMock.Setup(c => c.ProductVariants).Returns(() => MockDbSet(_productVariants).Object);

        var claims = new List<Claim> { new(ClaimNames.ID, "1"), new(ClaimNames.OFFICE_ID, "1") };
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) };
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        var mockDbContext = new Mock<DbContext>();
        var mockDatabaseFacade = new Mock<DatabaseFacade>(mockDbContext.Object);
        var mockTransaction = new Mock<IDbContextTransaction>();
        mockDatabaseFacade.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockTransaction.Object);
        _contextMock.Setup(c => c.Database).Returns(mockDatabaseFacade.Object);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _sut = new ReturnToSupplierOrderService(_contextMock.Object, _httpContextAccessorMock.Object, _loggerMock.Object);

        _suppliers.Add(new global::Backend.Domain.Entities.Supplier { Id = 7, Name = "NCC A" });
        _warehouses.Add(new global::Backend.Domain.Entities.Warehouse { Id = 1, Name = "Kho 1" });
        _productVariants.Add(new ProductVariant { Id = 5, SKU = "PV-05", Name = "Bao bì lỗi" });
    }

    private static Mock<DbSet<T>> MockDbSet<T>(List<T> list) where T : class
    {
        var q = list.AsQueryable().BuildMock();
        var set = new Mock<DbSet<T>>();
        set.As<IQueryable<T>>().Setup(m => m.Provider).Returns(q.Provider);
        set.As<IQueryable<T>>().Setup(m => m.Expression).Returns(q.Expression);
        set.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(q.ElementType);
        set.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => q.GetEnumerator());
        set.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(((IAsyncEnumerable<T>)q).GetAsyncEnumerator(default));
        set.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(list.Add);
        set.Setup(d => d.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Callback<T, CancellationToken>((item, _) => list.Add(item))
            .ReturnsAsync((T item, CancellationToken _) => null!);
        return set;
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsCreated()
    {
        var dto = new CreateReturnToSupplierOrderDto
        {
            WarehouseId = 1,
            SupplierId = 7,
            Note = "Trả hàng lỗi",
            Items = new List<CreateReturnToSupplierOrderItemDto>
            {
                new() { ProductVariantId = 5, QuarantineLocationId = 99, QuantityToReturn = 10, DamageReason = "Rách bao" }
            }
        };

        var result = await _sut.CreateAsync(dto);

        result.Status.Should().Be(201);
        _orders.Should().ContainSingle();
        _orders[0].SupplierId.Should().Be(7);
        _orders[0].ReturnCode.Should().StartWith("RTS-");
        _statuses.Should().ContainSingle(s => s.Code == ReturnToSupplierOrderStatusNames.Draft); // lazy-seed
    }

    [Fact]
    public async Task CreateAsync_EmptyItems_ReturnsBadRequest()
    {
        var dto = new CreateReturnToSupplierOrderDto { WarehouseId = 1, SupplierId = 7, Items = new() };
        var result = await _sut.CreateAsync(dto);
        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_SupplierNotFound_ReturnsNotFound()
    {
        var dto = new CreateReturnToSupplierOrderDto
        {
            WarehouseId = 1,
            SupplierId = 999,
            Items = new List<CreateReturnToSupplierOrderItemDto>
            {
                new() { ProductVariantId = 5, QuarantineLocationId = 99, QuantityToReturn = 10 }
            }
        };
        var result = await _sut.CreateAsync(dto);
        result.Status.Should().Be(404);
    }

    // ── Approve ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAsync_DraftOrder_MovesToApproved()
    {
        var draft = new ReturnToSupplierOrderStatus { Id = 1, Name = "Chờ duyệt", Code = ReturnToSupplierOrderStatusNames.Draft, Color = "#f59e0b" };
        _statuses.Add(draft);
        var order = new ReturnToSupplierOrder { Id = 1, SupplierId = 7, WarehouseId = 1, ReturnCode = "RTS-1", ReturnToSupplierOrderStatusId = 1, ReturnToSupplierOrderStatus = draft };
        _orders.Add(order);

        var result = await _sut.ApproveAsync(1, "OK");

        result.Status.Should().Be(200);
        order.ApprovedDate.Should().NotBeNull();
        _statuses.Should().Contain(s => s.Code == ReturnToSupplierOrderStatusNames.Approved);
    }

    [Fact]
    public async Task ApproveAsync_NonDraft_ReturnsUnprocessable()
    {
        var completed = new ReturnToSupplierOrderStatus { Id = 3, Name = "Hoàn thành", Code = ReturnToSupplierOrderStatusNames.Completed, Color = "#16a34a" };
        _statuses.Add(completed);
        _orders.Add(new ReturnToSupplierOrder { Id = 1, ReturnToSupplierOrderStatusId = 3, ReturnToSupplierOrderStatus = completed });

        var result = await _sut.ApproveAsync(1, null);
        result.Status.Should().Be(422);
    }

    // ── Confirm ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmAsync_HappyPath_ExportsStock_ReducesLotAndPayable_Completes()
    {
        var approved = new ReturnToSupplierOrderStatus { Id = 2, Name = "Đã duyệt", Code = ReturnToSupplierOrderStatusNames.Approved, Color = "#3b82f6" };
        _statuses.Add(approved);
        // Seed sẵn status "Hoàn thành" (Id=3) để EnsureStatus tìm thấy thay vì tạo mới Id=0
        _statuses.Add(new ReturnToSupplierOrderStatus { Id = 3, Name = "Hoàn thành", Code = ReturnToSupplierOrderStatusNames.Completed, Color = "#16a34a" });

        var item = new ReturnToSupplierOrderItem { Id = 11, ProductVariantId = 5, QuarantineLocationId = 99, QuantityToReturn = 10 };
        var order = new ReturnToSupplierOrder
        {
            Id = 1, SupplierId = 7, WarehouseId = 1, ReturnCode = "RTS-1",
            ReturnToSupplierOrderStatusId = 2, ReturnToSupplierOrderStatus = approved
        };
        order.Items.Add(item);
        _orders.Add(order);

        _locations.Add(new Location { Id = 99, IsQuarantine = true, CurrentOccupancy = 30m });
        _paddyLots.Add(new global::Backend.Domain.Entities.PaddyLot { Id = 500, LotCode = "LOT-500", RemainingWeightKg = 50m, InitialWeightKg = 100m });
        _inventories.Add(new global::Backend.Domain.Entities.Inventory
        {
            Id = 1, WarehouseId = 1, LocationId = 99, ProductVariantId = 5, PaddyLotId = 500,
            QuantityOnHand = 20m, QuantityReserved = 0m, CostPrice = 4000m, CreatedDate = DateTime.Now
        });
        _partyDebts.Add(new global::Backend.Domain.Entities.PartyDebt
        {
            Id = 1, PartyType = "SUPPLIER", PartyId = 7, Direction = "PAYABLE", CurrentBalance = 1_000_000m, IsActive = true
        });

        var result = await _sut.ConfirmAsync(1);

        result.Status.Should().Be(200);
        _inventories[0].QuantityOnHand.Should().Be(10m);              // 20 - 10
        _paddyLots[0].RemainingWeightKg.Should().Be(40m);             // 50 - 10
        _locations[0].CurrentOccupancy.Should().Be(20m);             // 30 - 10
        _partyDebts[0].CurrentBalance.Should().Be(960_000m);          // 1_000_000 - (10 * 4000)
        _inventoryTransactions.Should().Contain(t => t.ReferenceType == "RETURN_TO_SUPPLIER");
        _debtTransactions.Should().Contain(t => t.RefType == "RETURN_TO_SUPPLIER");
        order.ReturnToSupplierOrderStatusId.Should().Be(3); // chuyển sang "Hoàn thành"
        order.CompletedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfirmAsync_InsufficientStock_ReturnsUnprocessable()
    {
        var approved = new ReturnToSupplierOrderStatus { Id = 2, Name = "Đã duyệt", Code = ReturnToSupplierOrderStatusNames.Approved, Color = "#3b82f6" };
        _statuses.Add(approved);

        var item = new ReturnToSupplierOrderItem { Id = 11, ProductVariantId = 5, QuarantineLocationId = 99, QuantityToReturn = 50 };
        var order = new ReturnToSupplierOrder { Id = 1, SupplierId = 7, WarehouseId = 1, ReturnCode = "RTS-1", ReturnToSupplierOrderStatusId = 2, ReturnToSupplierOrderStatus = approved };
        order.Items.Add(item);
        _orders.Add(order);

        _inventories.Add(new global::Backend.Domain.Entities.Inventory
        { Id = 1, WarehouseId = 1, LocationId = 99, ProductVariantId = 5, QuantityOnHand = 5m, QuantityReserved = 0m, CostPrice = 4000m, CreatedDate = DateTime.Now });

        var result = await _sut.ConfirmAsync(1);
        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyCompleted_ReturnsBadRequest()
    {
        var completed = new ReturnToSupplierOrderStatus { Id = 3, Name = "Hoàn thành", Code = ReturnToSupplierOrderStatusNames.Completed, Color = "#16a34a" };
        _statuses.Add(completed);
        _orders.Add(new ReturnToSupplierOrder { Id = 1, ReturnToSupplierOrderStatusId = 3, ReturnToSupplierOrderStatus = completed });

        var result = await _sut.ConfirmAsync(1);
        result.Status.Should().Be(400);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_DraftOrder_MovesToCancelled()
    {
        var draft = new ReturnToSupplierOrderStatus { Id = 1, Name = "Chờ duyệt", Code = ReturnToSupplierOrderStatusNames.Draft, Color = "#f59e0b" };
        _statuses.Add(draft);
        var order = new ReturnToSupplierOrder { Id = 1, ReturnToSupplierOrderStatusId = 1, ReturnToSupplierOrderStatus = draft };
        _orders.Add(order);

        var result = await _sut.CancelAsync(1, "Nhầm");
        result.Status.Should().Be(200);
        _statuses.Should().Contain(s => s.Code == ReturnToSupplierOrderStatusNames.Cancelled);
    }

    [Fact]
    public async Task CancelAsync_Completed_ReturnsUnprocessable()
    {
        var completed = new ReturnToSupplierOrderStatus { Id = 3, Name = "Hoàn thành", Code = ReturnToSupplierOrderStatusNames.Completed, Color = "#16a34a" };
        _statuses.Add(completed);
        _orders.Add(new ReturnToSupplierOrder { Id = 1, ReturnToSupplierOrderStatusId = 3, ReturnToSupplierOrderStatus = completed });

        var result = await _sut.CancelAsync(1, "x");
        result.Status.Should().Be(422);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        var result = await _sut.GetByIdAsync(123);
        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSuccess()
    {
        var st = new ReturnToSupplierOrderStatus { Id = 1, Name = "Chờ duyệt", Code = ReturnToSupplierOrderStatusNames.Draft };
        _statuses.Add(st);
        _orders.Add(new ReturnToSupplierOrder { Id = 1, SupplierId = 7, WarehouseId = 1, ReturnCode = "RTS-1", ReturnToSupplierOrderStatusId = 1, ReturnToSupplierOrderStatus = st });

        var result = await _sut.GetAllAsync();
        result.Status.Should().Be(200);
    }
}
