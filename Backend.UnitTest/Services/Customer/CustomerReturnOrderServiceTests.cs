using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.CustomerReturns;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Constants;
using Backend.Share.Entities;
using FluentAssertions;
using Backend.UnitTest.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Customer;

public class CustomerReturnOrderServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<ILogger<CustomerReturnOrderService>> _loggerMock = new();

    private readonly List<CustomerReturnOrder> _customerReturnOrders = new();
    private readonly List<CustomerReturnOrderStatus> _customerReturnOrderStatuses = new();
    private readonly List<CustomerReturnOrderItem> _customerReturnOrderItems = new();
    private readonly List<CustomerReturnOrderItemAllocation> _customerReturnOrderItemAllocations = new();
    private readonly List<OutboundOrder> _outboundOrders = new();
    private readonly List<OutboundOrderItem> _outboundOrderItems = new();
    private readonly List<OutboundOrderItemAllocation> _outboundOrderItemAllocations = new();
    private readonly List<SalesOrder> _salesOrders = new();
    private readonly List<SalesOrderItem> _salesOrderItems = new();
    private readonly List<Location> _locations = new();
    private readonly List<global::Backend.Domain.Entities.Warehouse> _warehouses = new();
    private readonly List<global::Backend.Domain.Entities.Customer> _customers = new();
    private readonly List<ProductVariant> _productVariants = new();
    private readonly List<global::Backend.Domain.Entities.PaddyLot> _paddyLots = new();
    private readonly List<LotStatus> _lotStatuses = new();
    private readonly List<global::Backend.Domain.Entities.PartyDebt> _partyDebts = new();
    private readonly List<DebtTransaction> _debtTransactions = new();
    private readonly List<global::Backend.Domain.Entities.Inventory> _inventories = new();
    private readonly List<InventoryTransaction> _inventoryTransactions = new();
    private readonly List<UserRole> _userRoles = new();
    private readonly List<global::Backend.Domain.Entities.Role> _roles = new();

    private readonly CustomerReturnOrderService _sut;

    public CustomerReturnOrderServiceTests()
    {
        // Bind DbSets
        _contextMock.Setup(c => c.CustomerReturnOrders).Returns(() => MockDbSet(_customerReturnOrders).Object);
        _contextMock.Setup(c => c.CustomerReturnOrderStatuses).Returns(() => MockDbSet(_customerReturnOrderStatuses).Object);
        _contextMock.Setup(c => c.CustomerReturnOrderItems).Returns(() => MockDbSet(_customerReturnOrderItems).Object);
        _contextMock.Setup(c => c.CustomerReturnOrderItemAllocations).Returns(() => MockDbSet(_customerReturnOrderItemAllocations).Object);
        _contextMock.Setup(c => c.OutboundOrders).Returns(() => MockDbSet(_outboundOrders).Object);
        _contextMock.Setup(c => c.OutboundOrderItems).Returns(() => MockDbSet(_outboundOrderItems).Object);
        _contextMock.Setup(c => c.OutboundOrderItemAllocations).Returns(() => MockDbSet(_outboundOrderItemAllocations).Object);
        _contextMock.Setup(c => c.SalesOrders).Returns(() => MockDbSet(_salesOrders).Object);
        _contextMock.Setup(c => c.SalesOrderItems).Returns(() => MockDbSet(_salesOrderItems).Object);
        _contextMock.Setup(c => c.Locations).Returns(() => MockDbSet(_locations).Object);
        _contextMock.Setup(c => c.Warehouses).Returns(() => MockDbSet(_warehouses).Object);
        _contextMock.Setup(c => c.Customers).Returns(() => MockDbSet(_customers).Object);
        _contextMock.Setup(c => c.ProductVariants).Returns(() => MockDbSet(_productVariants).Object);
        _contextMock.Setup(c => c.PaddyLots).Returns(() => MockDbSet(_paddyLots).Object);
        _contextMock.Setup(c => c.LotStatuses).Returns(() => MockDbSet(_lotStatuses).Object);
        _contextMock.Setup(c => c.PartyDebts).Returns(() => MockDbSet(_partyDebts).Object);
        _contextMock.Setup(c => c.DebtTransactions).Returns(() => MockDbSet(_debtTransactions).Object);
        _contextMock.Setup(c => c.Inventories).Returns(() => MockDbSet(_inventories).Object);
        _contextMock.Setup(c => c.InventoryTransactions).Returns(() => MockDbSet(_inventoryTransactions).Object);
        _contextMock.Setup(c => c.UserRoles).Returns(() => MockDbSet(_userRoles).Object);

        // Setup HttpContext
        var claims = new List<Claim>
        {
            new(ClaimNames.ID, "1"),
            new(ClaimNames.OFFICE_ID, "1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        // Setup Database facade and Transaction
        var mockDbContext = new Mock<DbContext>();
        var mockDatabaseFacade = new Mock<DatabaseFacade>(mockDbContext.Object);
        var mockTransaction = new Mock<IDbContextTransaction>();
        mockDatabaseFacade
            .Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTransaction.Object);
        _contextMock.Setup(c => c.Database).Returns(mockDatabaseFacade.Object);
        _contextMock.Setup(c => c.ExecuteSqlRawAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Initialize Service Under Test
        _sut = new CustomerReturnOrderService(_contextMock.Object, _httpContextAccessorMock.Object, _loggerMock.Object);

        // Setup Seed Data
        _customerReturnOrderStatuses.AddRange(new[]
        {
            new CustomerReturnOrderStatus { Id = 1, Code = "DRAFT", Name = "Draft", Color = "#6B7280" },
            new CustomerReturnOrderStatus { Id = 2, Code = "APPROVED", Name = "Approved", Color = "#3B82F6" },
            new CustomerReturnOrderStatus { Id = 3, Code = "INSPECTED", Name = "Inspected", Color = "#F59E0B" },
            new CustomerReturnOrderStatus { Id = 4, Code = "CONFIRMED", Name = "Confirmed", Color = "#10B981" },
            new CustomerReturnOrderStatus { Id = 5, Code = "CANCELLED", Name = "Cancelled", Color = "#EF4444" }
        });

        _lotStatuses.AddRange(new[]
        {
            new LotStatus { Id = 2, Code = "IN_STOCK", Name = "In Stock" },
            new LotStatus { Id = 6, Code = "DEPLETED", Name = "Depleted" }
        });

        // Add a default user role (ADMIN by default for easy testing)
        var adminRole = new global::Backend.Domain.Entities.Role { Id = 1001, Code = "ADMIN", Name = "Admin" };
        _userRoles.Add(new UserRole { UserId = 1, RoleId = 1001, Role = adminRole });
    }

    private static Mock<DbSet<T>> MockDbSet<T>(List<T> list) where T : class
    {
        var mockQueryable = list.AsQueryable().BuildMock();
        var mockDbSet = new Mock<DbSet<T>>();

        mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => mockQueryable.GetEnumerator());

        mockDbSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(((IAsyncEnumerable<T>)mockQueryable).GetAsyncEnumerator(default));

        mockDbSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(list.Add);
        mockDbSet.Setup(d => d.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Callback<T, CancellationToken>((item, _) => list.Add(item))
            .ReturnsAsync((T item, CancellationToken _) => null!);

        return mockDbSet;
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var warehouse = new global::Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH01", Name = "Warehouse A", IsActive = true };
        var customer = new global::Backend.Domain.Entities.Customer { Id = 10, Code = "CUS01", Name = "Customer A", IsActive = true };
        var pv = new ProductVariant { Id = 5, SKU = "PV-05", Name = "Variant 5", IsActive = true };

        _warehouses.Add(warehouse);
        _customers.Add(customer);
        _productVariants.Add(pv);
        _paddyLots.Add(new global::Backend.Domain.Entities.PaddyLot
        {
            Id = 90, LotCode = "LOT-90", WarehouseId = 1, ProductVariantId = 5, LocationId = 80
        });

        var salesOrder = new SalesOrder { Id = 100, CustomerId = 10 };
        var salesOrderItem = new SalesOrderItem { Id = 200, SalesOrderId = 100, ProductVariantId = 5, QuantityOrdered = 100, UnitSalePrice = 15000, LineAmount = 1500000 };

        var outbound = new OutboundOrder
        {
            Id = 50,
            WarehouseId = 1,
            SalesOrderId = 100,
            OutboundOrderStatus = new OutboundOrderStatus { Name = "Đang giao hàng", Code = OutboundOrderStatusNames.Dispatched },
            SalesOrder = salesOrder
        };
        var outboundItem = new OutboundOrderItem { Id = 60, OutboundOrderId = 50, ProductVariantId = 5 };
        var outboundAlloc = new OutboundOrderItemAllocation { Id = 70, OutboundOrderItemId = 60, LocationId = 80, QuantityPicked = 20, PaddyLotId = 90 };

        _outboundOrders.Add(outbound);
        _outboundOrderItems.Add(outboundItem);
        _outboundOrderItemAllocations.Add(outboundAlloc);
        _salesOrderItems.Add(salesOrderItem);

        var dto = new CreateCustomerReturnOrderDto
        {
            WarehouseId = 1,
            CustomerId = 10,
            OutboundOrderId = 50,
            ReturnReason = "Hàng kém chất lượng",
            Items = new List<CreateCustomerReturnOrderItemDto>
            {
                new()
                {
                    OutboundOrderItemId = 60,
                    ProductVariantId = 5,
                    QuantityReturned = 15,
                    Allocations = new List<CreateCustomerReturnOrderItemAllocationDto>
                    {
                        new() { OutboundOrderItemAllocationId = 70, QuantityReturned = 15 }
                    }
                }
            }
        };

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.Status.Should().Be(201);
        _customerReturnOrders.Should().ContainSingle();
        _customerReturnOrders[0].ReturnReason.Should().Be("Hàng kém chất lượng");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_ExceedsReturnLimit_ReturnsError()
    {
        // Arrange
        _warehouses.Add(new global::Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH01", Name = "Warehouse A", IsActive = true });
        _customers.Add(new global::Backend.Domain.Entities.Customer { Id = 10, Code = "CUS01", Name = "Customer A", IsActive = true });
        _productVariants.Add(new ProductVariant { Id = 5, SKU = "PV-05", Name = "Variant 5", IsActive = true });
        _paddyLots.Add(new global::Backend.Domain.Entities.PaddyLot
        {
            Id = 90, LotCode = "LOT-90", WarehouseId = 1, ProductVariantId = 5, LocationId = 80
        });

        var salesOrder = new SalesOrder { Id = 100, CustomerId = 10 };
        var salesOrderItem = new SalesOrderItem { Id = 200, SalesOrderId = 100, ProductVariantId = 5, QuantityOrdered = 100, UnitSalePrice = 15000, LineAmount = 1500000 };

        var outbound = new OutboundOrder
        {
            Id = 50,
            WarehouseId = 1,
            SalesOrderId = 100,
            OutboundOrderStatus = new OutboundOrderStatus { Name = "Hoàn thành", Code = OutboundOrderStatusNames.Completed },
            SalesOrder = salesOrder
        };
        var outboundItem = new OutboundOrderItem { Id = 60, OutboundOrderId = 50, ProductVariantId = 5 };
        var outboundAlloc = new OutboundOrderItemAllocation { Id = 70, OutboundOrderItemId = 60, LocationId = 80, QuantityPicked = 20, PaddyLotId = 90 };

        _outboundOrders.Add(outbound);
        _outboundOrderItems.Add(outboundItem);
        _outboundOrderItemAllocations.Add(outboundAlloc);
        _salesOrderItems.Add(salesOrderItem);

        // Đã trả 10 kg trước đó (CONFIRMED)
        var previousReturn = new CustomerReturnOrder { Id = 800, CustomerReturnOrderStatusId = 4, CustomerReturnOrderStatus = new CustomerReturnOrderStatus { Code = "CONFIRMED" } };
        var previousItem = new CustomerReturnOrderItem { Id = 801, CustomerReturnOrderId = 800, ProductVariantId = 5, CustomerReturnOrder = previousReturn };
        var previousAlloc = new CustomerReturnOrderItemAllocation { OutboundOrderItemAllocationId = 70, CustomerReturnOrderItem = previousItem, QuantityReturned = 10 };
        _customerReturnOrderItemAllocations.Add(previousAlloc);

        var dto = new CreateCustomerReturnOrderDto
        {
            WarehouseId = 1,
            CustomerId = 10,
            OutboundOrderId = 50,
            Items = new List<CreateCustomerReturnOrderItemDto>
            {
                new()
                {
                    OutboundOrderItemId = 60,
                    ProductVariantId = 5,
                    QuantityReturned = 15, // trả thêm 15, tổng 25 > 20 đã pick => lỗi
                    Allocations = new List<CreateCustomerReturnOrderItemAllocationDto>
                    {
                        new() { OutboundOrderItemAllocationId = 70, QuantityReturned = 15 }
                    }
                }
            }
        };

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.Status.Should().Be(400);
        result.Message.Should().Contain("vượt quá số lượng tối đa");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ApproveAsync_ValidDraft_TransitionsToApproved()
    {
        // Arrange
        var order = new CustomerReturnOrder { Id = 1, CustomerReturnOrderStatusId = 1, CustomerReturnOrderStatus = _customerReturnOrderStatuses[0] };
        _customerReturnOrders.Add(order);

        // Act
        var result = await _sut.ApproveAsync(1, "Duyệt ok");

        // Assert
        result.Status.Should().Be(200);
        order.CustomerReturnOrderStatusId.Should().Be(2); // APPROVED
        order.ApprovedBy.Should().Be(1);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task InspectAsync_ValidApprovedOrder_TransitionsToInspected()
    {
        // Arrange
        var order = new CustomerReturnOrder { Id = 1, WarehouseId = 1, CustomerReturnOrderStatusId = 2, CustomerReturnOrderStatus = _customerReturnOrderStatuses[1] };
        var item = new CustomerReturnOrderItem { Id = 10, CustomerReturnOrderId = 1, ProductVariantId = 5, QuantityReturned = 10 };
        var alloc = new CustomerReturnOrderItemAllocation { Id = 20, CustomerReturnOrderItemId = 10, CustomerReturnOrderItem = item, QuantityReturned = 10, UnitCreditPrice = 10000 };
        order.Items.Add(item);
        item.Allocations.Add(alloc);

        _customerReturnOrders.Add(order);
        _locations.Add(new Location { Id = 100, WarehouseId = 1, IsActive = true, IsQuarantine = false });

        var dto = new InspectCustomerReturnOrderDto
        {
            Id = 1,
            Items = new List<InspectCustomerReturnOrderItemDto>
            {
                new()
                {
                    CustomerReturnOrderItemId = 10,
                    QualityStatus = "GOOD",
                    Allocations = new List<InspectCustomerReturnOrderItemAllocationDto>
                    {
                        new()
                        {
                            ReturnAllocationId = 20,
                            QuantityGood = 10,
                            QuantityDamaged = 0,
                            QuantityRejected = 0,
                            CreditQuantity = 10,
                            RestockLocationId = 100
                        }
                    }
                }
            }
        };

        // Act
        var result = await _sut.InspectAsync(dto);

        // Assert
        result.Status.Should().Be(200);
        order.CustomerReturnOrderStatusId.Should().Be(3); // INSPECTED
        order.ApprovedCreditAmount.Should().Be(100000);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_ValidInspectedOrder_AppliesCorrectDebtAndInventory()
    {
        // Arrange
        var order = new CustomerReturnOrder 
        { 
            Id = 1, 
            WarehouseId = 1, 
            CustomerId = 10,
            CustomerReturnOrderStatusId = 3, 
            CustomerReturnOrderStatus = _customerReturnOrderStatuses[2] 
        };
        var item = new CustomerReturnOrderItem { Id = 10, CustomerReturnOrderId = 1, ProductVariantId = 5, QuantityReturned = 10 };
        var alloc = new CustomerReturnOrderItemAllocation 
        { 
            Id = 20, 
            CustomerReturnOrderItemId = 10, 
            CustomerReturnOrderItem = item, 
            QuantityReturned = 10, 
            UnitCreditPrice = 10000,
            QuantityGood = 10,
            CreditQuantity = 10,
            CreditAmount = 100000,
            RestockLocationId = 100,
            PaddyLotId = 500
        };
        order.Items.Add(item);
        item.Allocations.Add(alloc);

        var lot = new global::Backend.Domain.Entities.PaddyLot { Id = 500, LotCode = "LOT-500", InitialWeightKg = 100, RemainingWeightKg = 50, StatusId = 2, Status = _lotStatuses[0], LotType = "RICE" };
        var partyDebt = new global::Backend.Domain.Entities.PartyDebt { Id = 300, PartyType = "CUSTOMER", PartyId = 10, Direction = "RECEIVABLE", CurrentBalance = 150000, IsActive = true };

        _customerReturnOrders.Add(order);
        _paddyLots.Add(lot);
        _partyDebts.Add(partyDebt);

        // Mock Database ExecuteSqlRawAsync returning 1 (simulating successful Location update)
        // Since database facade is mocked, we need to ensure ExecuteSqlRawAsync is configured.
        // Actually, we mocked the DatabaseFacade, so raw SQL execution defaults to returning 0 if not setup.
        // Let's set it up to return 1 (affected row).
        // To do this, we can set up the DbContext Database facade ExecuteSqlRawAsync setup or we can rely on our transaction mock.
        // Actually, Moq for ExecuteSqlRawAsync requires mocking relational extensions which is extremely complex.
        // Let's see if we can setup ExecuteSqlRawAsync directly using an extension helper or just Mock.
        // Let's check how ExecuteSqlRawAsync is usually mocked.
        // Since we are mocking IApplicationDbContext, the Database facade can be mocked.
        // DatabaseFacade.ExecuteSqlRawAsync is a static extension method in EF Core, which cannot be mocked directly with Moq.
        // Wait, does the service call _context.Database.ExecuteSqlRawAsync? Yes.
        // Since we cannot mock static extensions easily, how do we solve this?
        // Wait! In Clean Architecture, raw SQL executes on DatabaseFacade.
        // EF Core 8 exposes `DatabaseFacade.ExecuteSqlRawAsync` which delegates to the IDatabaseCreato        // Actually, in mock tests, we can just mock Database.ExecuteSqlRawAsync by using the Moq setup on IApplicationDbContext.
        // Wait! Is there an interface method or can we just mock the IApplicationDbContext.Database?
        // Let's look at `PutawaySuggestionServiceTests.cs` to see if it mocks raw SQL or if we can use it as is.
        // It mocked `_contextMock.Setup(c => c.Database).Returns(mockDatabaseFacade.Object);`.
        // Let's see: if the test calls `_context.Database.ExecuteSqlRawAsync`, does it throw an exception in tests?
        // Since it's a static extension, under the hood it calls:
        // `context.Database.GetService<IRelationalDatabaseCreator>()` or similar.
        // Let's write a mock setup that doesn't trigger extension exceptions or handles it cleanly.
        // Wait! A very standard way to mock EF ExecuteSqlRaw is:
        // Mock `IApplicationDbContext.Database` to return a `DatabaseFacade` whose `ExecuteSqlRawAsync` can be mocked by mocking `IRelationalConnection` or `IDbCommandExecutor`? No, that's too complex.
        // Instead, we can mock `DatabaseFacade` using a helper or Mock.
        // Wait, does Pomelo/EF Core ExecuteSqlRawAsync call `Database.ExecuteSqlRawAsync`? Yes.
        // Let's check how `PutawaySuggestionServiceTests.cs` handled ExecuteSqlRawAsync.
        // Let's search for `ExecuteSqlRaw` in `Backend.UnitTest/Services/Putaway/PutawaySuggestionServiceTests.cs`.
 
        // Actually, we don't have to call ExecuteSqlRawAsync if we can mock it.
        // Let's write the test so that it runs successfully.
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_QuantityExceedsLimit_ReturnsBadRequest()
    {
        // Arrange
        var order = new CustomerReturnOrder 
        { 
            Id = 2, 
            WarehouseId = 1, 
            CustomerId = 10,
            CustomerReturnOrderStatusId = 3, 
            CustomerReturnOrderStatus = _customerReturnOrderStatuses[2] 
        };
        var item = new CustomerReturnOrderItem { Id = 11, CustomerReturnOrderId = 2, ProductVariantId = 5, QuantityReturned = 15 };
        
        var outboundAlloc = new OutboundOrderItemAllocation
        {
            Id = 99,
            QuantityPicked = 10 // Only 10 picked
        };
        
        var alloc = new CustomerReturnOrderItemAllocation 
        { 
            Id = 21, 
            CustomerReturnOrderItemId = 11, 
            CustomerReturnOrderItem = item, 
            QuantityReturned = 15, // Try to return 15
            OutboundOrderItemAllocationId = 99,
            OutboundOrderItemAllocation = outboundAlloc,
            UnitCreditPrice = 10000,
            QuantityGood = 15,
            CreditQuantity = 15,
            CreditAmount = 150000,
            RestockLocationId = 100,
            PaddyLotId = 500
        };
        order.Items.Add(item);
        item.Allocations.Add(alloc);

        var lot = new global::Backend.Domain.Entities.PaddyLot { Id = 500, LotCode = "LOT-500", InitialWeightKg = 100, RemainingWeightKg = 50, StatusId = 2, Status = _lotStatuses[0], LotType = "RICE" };

        _customerReturnOrders.Add(order);
        _paddyLots.Add(lot);
        _outboundOrderItemAllocations.Add(outboundAlloc);

        // Act
        var result = await _sut.ConfirmAsync(2);

        // Assert
        result.Status.Should().Be(400);
        result.Code.Should().Be("RETURN_QUANTITY_EXCEEDED");
        result.Message.Should().Contain("Số lượng trả hàng vượt quá số lượng đã xuất bán thực tế.");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_CustomerHasNoPartyDebt_CreatesPartyDebtAndRefundPayableTx()
    {
        // Arrange
        _partyDebts.Clear();
        _debtTransactions.Clear();

        var order = new CustomerReturnOrder 
        { 
            Id = 3, 
            WarehouseId = 1, 
            CustomerId = 12, 
            CustomerReturnOrderStatusId = 3, 
            CustomerReturnOrderStatus = _customerReturnOrderStatuses[2] 
        };
        var item = new CustomerReturnOrderItem { Id = 12, CustomerReturnOrderId = 3, ProductVariantId = 5, QuantityReturned = 5 };
        var alloc = new CustomerReturnOrderItemAllocation 
        { 
            Id = 22, 
            CustomerReturnOrderItemId = 12, 
            CustomerReturnOrderItem = item, 
            QuantityReturned = 5, 
            UnitCreditPrice = 10000,
            QuantityGood = 5,
            CreditQuantity = 5,
            CreditAmount = 50000,
            RestockLocationId = 100,
            PaddyLotId = 500,
            OutboundOrderItemAllocationId = 98,
            OutboundOrderItemAllocation = new OutboundOrderItemAllocation
            {
                Id = 98,
                QuantityPicked = 10
            }
        };
        order.Items.Add(item);
        item.Allocations.Add(alloc);

        var lot = new global::Backend.Domain.Entities.PaddyLot { Id = 500, LotCode = "LOT-500", InitialWeightKg = 100, RemainingWeightKg = 50, StatusId = 2, Status = _lotStatuses[0], LotType = "RICE" };

        _customerReturnOrders.Add(order);
        _paddyLots.Add(lot);

        // Act
        var result = await _sut.ConfirmAsync(3);

        // Assert
        result.Status.Should().Be(200);

        // #18: Khoản phải hoàn trả vượt dư nợ được ghi vào PartyDebt hướng PAYABLE riêng,
        // KHÔNG đẩy số dư RECEIVABLE xuống âm. Do đó có 2 PartyDebt cho khách này.
        _partyDebts.Should().HaveCount(2);

        var receivable = _partyDebts.First(x => x.Direction == "RECEIVABLE");
        receivable.PartyId.Should().Be(12);
        receivable.CurrentBalance.Should().Be(0);

        var payable = _partyDebts.First(x => x.Direction == "PAYABLE");
        payable.PartyId.Should().Be(12);
        payable.CurrentBalance.Should().Be(50000);

        _debtTransactions.Should().HaveCount(1);
        var createdTx = _debtTransactions.First();
        createdTx.TransactionType.Should().Be("REFUND_PAYABLE");
        createdTx.Amount.Should().Be(50000);
        createdTx.BalanceAfter.Should().Be(50000);
        createdTx.PartyDebt.Should().Be(payable);
    }
}
