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
    private readonly List<global::Backend.Domain.Entities.CustomerFeedback> _customerFeedbacks = new();
    private readonly List<PaddyLotBag> _paddyLotBags = new();
    private readonly List<PaddyLotBagContent> _paddyLotBagContents = new();
    private readonly List<PaddyLotBagMovement> _paddyLotBagMovements = new();

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
        _contextMock.Setup(c => c.CustomerFeedbacks).Returns(() => MockDbSet(_customerFeedbacks).Object);
        _contextMock.Setup(c => c.PaddyLotBags).Returns(() => MockDbSet(_paddyLotBags).Object);
        _contextMock.Setup(c => c.PaddyLotBagContents).Returns(() => MockDbSet(_paddyLotBagContents).Object);
        _contextMock.Setup(c => c.PaddyLotBagMovements).Returns(() => MockDbSet(_paddyLotBagMovements).Object);

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
            ,new CustomerReturnOrderStatus { Id = 6, Code = "PENDING_APPROVAL", Name = "Pending approval", Color = "#8B5CF6" }
            ,new CustomerReturnOrderStatus { Id = 7, Code = "RECEIVED", Name = "Received", Color = "#06B6D4" }
            ,new CustomerReturnOrderStatus { Id = 8, Code = "REJECTED", Name = "Rejected", Color = "#DC2626" }
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

    private InspectCustomerReturnOrderDto ArrangeInspectionLocationScenario(
        int orderId, decimal occupancy, int? currentProductVariantId)
    {
        var order = new CustomerReturnOrder
        {
            Id = orderId,
            OrganizationId = 1,
            WarehouseId = 1,
            CustomerReturnOrderStatusId = 7,
            CustomerReturnOrderStatus = _customerReturnOrderStatuses[6]
        };
        var item = new CustomerReturnOrderItem
        {
            Id = orderId * 10,
            CustomerReturnOrderId = orderId,
            ProductVariantId = 5,
            QuantityReturned = 4m
        };
        var allocation = new CustomerReturnOrderItemAllocation
        {
            Id = orderId * 10 + 1,
            CustomerReturnOrderItemId = item.Id,
            CustomerReturnOrderItem = item,
            ProductVariantId = 5,
            QuantityReturned = 4m,
            QuantityReceived = 4m,
            UnitCreditPrice = 10000m
        };
        item.Allocations.Add(allocation);
        order.Items.Add(item);
        _customerReturnOrders.Add(order);
        _locations.Add(new Location
        {
            Id = orderId * 100,
            WarehouseId = 1,
            IsActive = true,
            IsQuarantine = false,
            CurrentOccupancy = occupancy,
            CurrentProductVariantId = currentProductVariantId,
            MaxCapacity = 100m
        });

        return new InspectCustomerReturnOrderDto
        {
            Id = orderId,
            Items = new List<InspectCustomerReturnOrderItemDto>
            {
                new()
                {
                    CustomerReturnOrderItemId = item.Id,
                    QualityStatus = "GOOD",
                    Allocations = new List<InspectCustomerReturnOrderItemAllocationDto>
                    {
                        new()
                        {
                            ReturnAllocationId = allocation.Id,
                            QuantityGood = 4m,
                            CreditQuantity = 4m,
                            RestockLocationId = orderId * 100
                        }
                    }
                }
            }
        };
    }

    private InspectCustomerReturnOrderDto ArrangeQuarantineLocationScenario(
        int orderId,
        decimal occupancy,
        int? currentProductVariantId,
        bool isSingleTypeColumn,
        decimal maxCapacity = 100m,
        decimal quantity = 10m)
    {
        var order = new CustomerReturnOrder
        {
            Id = orderId,
            OrganizationId = 1,
            WarehouseId = 1,
            CustomerReturnOrderStatusId = 7,
            CustomerReturnOrderStatus = _customerReturnOrderStatuses.Single(x => x.Code == "RECEIVED")
        };
        var item = new CustomerReturnOrderItem
        {
            Id = orderId * 10,
            CustomerReturnOrderId = orderId,
            ProductVariantId = 5,
            QuantityReturned = quantity
        };
        var allocation = new CustomerReturnOrderItemAllocation
        {
            Id = orderId * 10 + 1,
            CustomerReturnOrderItemId = item.Id,
            CustomerReturnOrderItem = item,
            ProductVariantId = 5,
            QuantityReturned = quantity,
            QuantityReceived = quantity,
            UnitCreditPrice = 10000m
        };
        item.Allocations.Add(allocation);
        order.Items.Add(item);
        _customerReturnOrders.Add(order);

        _locations.Add(new Location
        {
            Id = orderId * 100,
            WarehouseId = 1,
            IsActive = true,
            IsQuarantine = true,
            IsSingleTypeColumn = isSingleTypeColumn,
            CurrentOccupancy = occupancy,
            CurrentProductVariantId = currentProductVariantId,
            MaxCapacity = maxCapacity
        });

        return new InspectCustomerReturnOrderDto
        {
            Id = orderId,
            Items = new List<InspectCustomerReturnOrderItemDto>
            {
                new()
                {
                    CustomerReturnOrderItemId = item.Id,
                    QualityStatus = "DAMAGED",
                    DamageReason = "Bao rách",
                    Allocations = new List<InspectCustomerReturnOrderItemAllocationDto>
                    {
                        new()
                        {
                            ReturnAllocationId = allocation.Id,
                            QuantityDamaged = quantity,
                            CreditQuantity = quantity,
                            QuarantineLocationId = orderId * 100
                        }
                    }
                }
            }
        };
    }

    private void AddReturnReservation(
        string statusCode,
        decimal quantity,
        int outboundAllocationId = 70,
        int orderId = 800)
    {
        var status = _customerReturnOrderStatuses.Single(x => x.Code == statusCode);
        var order = new CustomerReturnOrder
        {
            Id = orderId,
            CustomerReturnOrderStatusId = status.Id,
            CustomerReturnOrderStatus = status
        };
        var item = new CustomerReturnOrderItem
        {
            Id = orderId * 10,
            CustomerReturnOrderId = orderId,
            CustomerReturnOrder = order,
            ProductVariantId = 5
        };
        _customerReturnOrderItemAllocations.Add(new CustomerReturnOrderItemAllocation
        {
            Id = orderId * 10 + 1,
            OutboundOrderItemAllocationId = outboundAllocationId,
            CustomerReturnOrderItemId = item.Id,
            CustomerReturnOrderItem = item,
            QuantityReturned = quantity
        });
    }

    private (CustomerReturnOrder Order, global::Backend.Domain.Entities.PaddyLot Lot, PaddyLotBag? ExistingOpenBag)
        ArrangeGoodReturnForConfirmation(
            int orderId, decimal quantity, bool withExistingOpenBag,
            decimal standardWeight = 10m, decimal existingOpenWeight = 6m)
    {
        var order = new CustomerReturnOrder
        {
            Id = orderId,
            OrganizationId = 1,
            WarehouseId = 1,
            CustomerId = 10,
            ReturnCode = $"CR-{orderId:D3}",
            CustomerReturnOrderStatusId = 3,
            CustomerReturnOrderStatus = _customerReturnOrderStatuses[2]
        };
        var item = new CustomerReturnOrderItem
        {
            Id = orderId * 10,
            CustomerReturnOrderId = orderId,
            ProductVariantId = 5,
            QuantityReturned = quantity
        };
        var allocation = new CustomerReturnOrderItemAllocation
        {
            Id = orderId * 10 + 1,
            CustomerReturnOrderItemId = item.Id,
            CustomerReturnOrderItem = item,
            ProductVariantId = 5,
            PaddyLotId = 500,
            QuantityReturned = quantity,
            QuantityGood = quantity,
            CreditQuantity = quantity,
            UnitCreditPrice = 10000m,
            CreditAmount = quantity * 10000m,
            RestockLocationId = 100
        };
        item.Allocations.Add(allocation);
        order.Items.Add(item);

        var lot = new global::Backend.Domain.Entities.PaddyLot
        {
            Id = 500,
            LotCode = "LOT-500",
            ProductVariantId = 5,
            InitialWeightKg = 500m,
            RemainingWeightKg = 50m,
            StatusId = 2,
            Status = _lotStatuses[0],
            LotType = "RICE"
        };
        _customerReturnOrders.Add(order);
        _paddyLots.Add(lot);
        _partyDebts.Add(new global::Backend.Domain.Entities.PartyDebt
        {
            Id = 300,
            PartyType = LookupCodes.PartyType.Customer,
            PartyId = 10,
            Direction = LookupCodes.DebtDirection.Receivable,
            CurrentBalance = 2000000m,
            IsActive = true
        });
        _productVariants.Add(new ProductVariant
        {
            Id = 5,
            SKU = "RICE-10",
            Name = "Gạo 10 kg",
            Weight = standardWeight
        });
        _locations.Add(new Location
        {
            Id = 100,
            WarehouseId = 1,
            IsActive = true,
            IsQuarantine = false,
            CurrentOccupancy = withExistingOpenBag ? existingOpenWeight : 0,
            CurrentProductVariantId = withExistingOpenBag ? 5 : null,
            MaxCapacity = 500m
        });

        if (!withExistingOpenBag)
            return (order, lot, null);

        var sourceLot = new global::Backend.Domain.Entities.PaddyLot
        {
            Id = 501,
            LotCode = "LOT-501",
            ProductVariantId = 5,
            InitialWeightKg = 100m,
            RemainingWeightKg = existingOpenWeight,
            StatusId = 2,
            Status = _lotStatuses[0],
            LotType = "RICE"
        };
        _paddyLots.Add(sourceLot);

        var existingOpenBag = new PaddyLotBag
        {
            Id = 900,
            LotId = 501,
            Lot = sourceLot,
            BagNo = 1,
            WeightKg = existingOpenWeight,
            LocationId = 100,
            Status = PaddyLotBagStatuses.Stored,
            IsFull = false,
            StandardWeightKg = standardWeight,
            BagKind = PaddyLotBagKinds.Finished,
            OpenBagKey = "5:1:100"
        };
        existingOpenBag.Contents.Add(new PaddyLotBagContent
        {
            BagId = 900,
            Bag = existingOpenBag,
            LotId = 501,
            WeightKg = existingOpenWeight
        });
        _paddyLotBags.Add(existingOpenBag);
        _paddyLotBagContents.Add(existingOpenBag.Contents.Single());
        return (order, lot, existingOpenBag);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_WithoutFeedback_DoesNotCreateFeedback()
    {
        var dto = ArrangeValidCreateDto();

        var result = await _sut.CreateAsync(dto);

        result.Status.Should().Be(201);
        _customerReturnOrders.Should().ContainSingle();
        _customerReturnOrders[0].ReturnReason.Should().Be("Hàng kém chất lượng");
        _customerReturnOrders[0].CustomerFeedbackId.Should().BeNull();
        _customerReturnOrders[0].CustomerFeedback.Should().BeNull();
        _customerFeedbacks.Should().BeEmpty();
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_WithValidFeedback_PreservesFeedbackLink()
    {
        var dto = ArrangeValidCreateDto();
        var feedback = new global::Backend.Domain.Entities.CustomerFeedback
        {
            Id = 700,
            SalesOrderId = 100,
            OutboundOrderId = 50,
            FeedbackType = "QUALITY",
            Description = "Gạo bị ẩm",
            ResolutionStatus = "OPEN"
        };
        _customerFeedbacks.Add(feedback);
        dto.CustomerFeedbackId = feedback.Id;

        var result = await _sut.CreateAsync(dto);

        result.Status.Should().Be(201);
        _customerReturnOrders.Should().ContainSingle();
        _customerReturnOrders[0].CustomerFeedbackId.Should().Be(feedback.Id);
        _customerReturnOrders[0].CustomerFeedback.Should().BeSameAs(feedback);
        _customerFeedbacks.Should().ContainSingle();
    }

    private CreateCustomerReturnOrderDto ArrangeValidCreateDto()
    {
        var warehouse = new global::Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH01", Name = "Warehouse A", IsActive = true };
        var customer = new global::Backend.Domain.Entities.Customer { Id = 10, OrganizationId = 1, Code = "CUS01", Name = "Customer A", IsActive = true };
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
            OrganizationId = 1,
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

        return new CreateCustomerReturnOrderDto
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
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_ExceedsReturnLimit_ReturnsError()
    {
        // Arrange
        _warehouses.Add(new global::Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH01", Name = "Warehouse A", IsActive = true });
        _customers.Add(new global::Backend.Domain.Entities.Customer { Id = 10, OrganizationId = 1, Code = "CUS01", Name = "Customer A", IsActive = true });
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
            OrganizationId = 1,
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

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("PENDING_APPROVAL")]
    [InlineData("APPROVED")]
    [InlineData("RECEIVED")]
    [InlineData("INSPECTED")]
    [InlineData("CONFIRMED")]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_ActiveReturnStatus_ReservesReturnedQuantity(string statusCode)
    {
        var dto = ArrangeValidCreateDto();
        AddReturnReservation(statusCode, quantity: 6m);

        var result = await _sut.CreateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("vượt quá số lượng tối đa");
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("REJECTED")]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_InactiveReturnStatus_ReleasesReturnedQuantity(string statusCode)
    {
        var dto = ArrangeValidCreateDto();
        AddReturnReservation(statusCode, quantity: 6m);

        var result = await _sut.CreateAsync(dto);

        result.Status.Should().Be(201);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task CreateAsync_DispatchedOutbound_ReturnsError()
    {
        var dto = ArrangeValidCreateDto();
        _outboundOrders.Single().OutboundOrderStatus.Code = OutboundOrderStatusNames.Dispatched;

        var result = await _sut.CreateAsync(dto);

        result.Status.Should().Be(422);
        result.Message.Should().Be("Chỉ được tạo phiếu trả hàng từ phiếu xuất đã giao thành công.");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task UpdateAsync_CurrentOrderReservation_IsExcluded()
    {
        var createDto = ArrangeValidCreateDto();
        var draft = _customerReturnOrderStatuses.Single(x => x.Code == "DRAFT");
        _customerReturnOrders.Add(new CustomerReturnOrder
        {
            Id = 900,
            OrganizationId = 1,
            WarehouseId = 1,
            CustomerId = 10,
            OutboundOrderId = 50,
            CustomerReturnOrderStatusId = draft.Id,
            CustomerReturnOrderStatus = draft
        });
        AddReturnReservation("DRAFT", quantity: 5m, orderId: 900);
        createDto.Items.Single().QuantityReturned = 7m;
        createDto.Items.Single().Allocations.Single().QuantityReturned = 7m;

        var result = await _sut.UpdateAsync(new UpdateCustomerReturnOrderDto
        {
            Id = 900,
            ReturnReason = "Điều chỉnh số lượng",
            Items = createDto.Items
        });

        result.Status.Should().Be(200);
        _customerReturnOrders.Single(x => x.Id == 900).Items
            .Single().QuantityReturned.Should().Be(7m);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ApproveAsync_ValidPendingApproval_TransitionsToApproved()
    {
        // Arrange
        var order = new CustomerReturnOrder { Id = 1, OrganizationId = 1, CustomerReturnOrderStatusId = 6, CustomerReturnOrderStatus = _customerReturnOrderStatuses[5] };
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
    public async Task InspectAsync_ValidReceivedOrder_TransitionsToInspected()
    {
        // Arrange
        var order = new CustomerReturnOrder { Id = 1, OrganizationId = 1, WarehouseId = 1, CustomerReturnOrderStatusId = 7, CustomerReturnOrderStatus = _customerReturnOrderStatuses[6] };
        var item = new CustomerReturnOrderItem { Id = 10, CustomerReturnOrderId = 1, ProductVariantId = 5, QuantityReturned = 10 };
        var alloc = new CustomerReturnOrderItemAllocation { Id = 20, CustomerReturnOrderItemId = 10, CustomerReturnOrderItem = item, QuantityReturned = 10, QuantityReceived = 10, UnitCreditPrice = 10000 };
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
    public async Task InspectAsync_EmptyLocation_AcceptsGoodReturn()
    {
        var dto = ArrangeInspectionLocationScenario(21, occupancy: 0m, currentProductVariantId: null);

        var result = await _sut.InspectAsync(dto);

        result.Status.Should().Be(200);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task InspectAsync_LocationWithSameSku_AcceptsGoodReturn()
    {
        var dto = ArrangeInspectionLocationScenario(22, occupancy: 6m, currentProductVariantId: 5);

        var result = await _sut.InspectAsync(dto);

        result.Status.Should().Be(200);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task InspectAsync_LocationWithDifferentSku_RejectsGoodReturn()
    {
        var dto = ArrangeInspectionLocationScenario(23, occupancy: 6m, currentProductVariantId: 6);

        var result = await _sut.InspectAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("cột trống hoặc đang chứa cùng SKU");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task InspectAsync_QuarantineLocationWithoutCapacity_ReturnsError()
    {
        var dto = ArrangeQuarantineLocationScenario(
            orderId: 24, occupancy: 95m, currentProductVariantId: 5,
            isSingleTypeColumn: true, maxCapacity: 100m, quantity: 10m);

        var result = await _sut.InspectAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("không còn đủ sức chứa");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task InspectAsync_SingleTypeQuarantineWithSameSku_Succeeds()
    {
        var dto = ArrangeQuarantineLocationScenario(
            orderId: 25, occupancy: 50m, currentProductVariantId: 5,
            isSingleTypeColumn: true);

        var result = await _sut.InspectAsync(dto);

        result.Status.Should().Be(200);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task InspectAsync_SingleTypeQuarantineWithDifferentSku_ReturnsError()
    {
        var dto = ArrangeQuarantineLocationScenario(
            orderId: 26, occupancy: 50m, currentProductVariantId: 6,
            isSingleTypeColumn: true);

        var result = await _sut.InspectAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("không tương thích SKU");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_ExistingOpenBag6AndReturn4_TopsUpAcrossLotsWithoutNewBag()
    {
        ArrangeGoodReturnForConfirmation(11, 4m, withExistingOpenBag: true);

        var result = await _sut.ConfirmAsync(11);

        result.Status.Should().Be(200);
        _paddyLotBags.Should().ContainSingle();
        var bag = _paddyLotBags.Single();
        bag.WeightKg.Should().Be(10m);
        bag.IsFull.Should().BeTrue();
        bag.OpenBagKey.Should().BeNull();
        bag.Contents.Should().Contain(c => c.LotId == 501 && c.WeightKg == 6m);
        bag.Contents.Should().Contain(c => c.LotId == 500 && c.WeightKg == 4m);
        bag.Movements.Should().ContainSingle(m =>
            m.BeforeWeightKg == 6m && m.AfterWeightKg == 10m && m.WeightKg == 4m);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_ExistingOpenBag6AndReturn7_CreatesOneOpenRemainder()
    {
        ArrangeGoodReturnForConfirmation(12, 7m, withExistingOpenBag: true);

        var result = await _sut.ConfirmAsync(12);

        result.Status.Should().Be(200);
        _paddyLotBags.Should().HaveCount(2);
        var toppedUp = _paddyLotBags.Single(x => x.Id == 900);
        toppedUp.WeightKg.Should().Be(10m);
        toppedUp.IsFull.Should().BeTrue();
        toppedUp.OpenBagKey.Should().BeNull();
        var remainder = _paddyLotBags.Single(x => x.Id != 900);
        remainder.WeightKg.Should().Be(3m);
        remainder.IsFull.Should().BeFalse();
        remainder.OpenBagKey.Should().Be("5:1:100");
        remainder.Contents.Should().ContainSingle(c => c.LotId == 500 && c.WeightKg == 3m);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_ExistingOpenBag6AndReturn14_TopsUpThenCreatesOneFullBag()
    {
        ArrangeGoodReturnForConfirmation(13, 14m, withExistingOpenBag: true);

        var result = await _sut.ConfirmAsync(13);

        result.Status.Should().Be(200);
        _paddyLotBags.Should().HaveCount(2);
        _paddyLotBags.Single(x => x.Id == 900).WeightKg.Should().Be(10m);
        var returnedBag = _paddyLotBags.Single(x => x.Id != 900);
        returnedBag.WeightKg.Should().Be(10m);
        returnedBag.IsFull.Should().BeTrue();
        returnedBag.OpenBagKey.Should().BeNull();
        _paddyLotBags.Should().NotContain(x => x.OpenBagKey != null);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_NoOpenBagAndReturn11_CreatesFullAndSingleOpenBag()
    {
        ArrangeGoodReturnForConfirmation(14, 11m, withExistingOpenBag: false);

        var result = await _sut.ConfirmAsync(14);

        result.Status.Should().Be(200);
        _paddyLotBags.Should().HaveCount(2);
        _paddyLotBags.Should().ContainSingle(x => x.IsFull && x.WeightKg == 10m);
        _paddyLotBags.Should().ContainSingle(x =>
            !x.IsFull && x.WeightKg == 1m && x.OpenBagKey == "5:1:100");
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_TwoAllocationsSameSkuAndLocation_TopUpPendingOpenBag()
    {
        var (order, _, _) = ArrangeGoodReturnForConfirmation(15, 3m, withExistingOpenBag: false);
        var item = order.Items.Single();
        item.Allocations.Add(new CustomerReturnOrderItemAllocation
        {
            Id = 152,
            CustomerReturnOrderItemId = item.Id,
            CustomerReturnOrderItem = item,
            ProductVariantId = 5,
            PaddyLotId = 500,
            QuantityReturned = 2m,
            QuantityGood = 2m,
            RestockLocationId = 100
        });

        var result = await _sut.ConfirmAsync(15);

        result.Status.Should().Be(200);
        _paddyLotBags.Should().ContainSingle();
        _paddyLotBags.Single().WeightKg.Should().Be(5m);
        _paddyLotBags.Single().OpenBagKey.Should().Be("5:1:100");
        _paddyLotBags.Count(x => x.OpenBagKey == "5:1:100").Should().Be(1);
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_LocationContainsDifferentSku_RejectsBeforeMutation()
    {
        ArrangeGoodReturnForConfirmation(16, 4m, withExistingOpenBag: false);
        var location = _locations.Single(x => x.Id == 100);
        location.CurrentOccupancy = 5m;
        location.CurrentProductVariantId = 6;

        var result = await _sut.ConfirmAsync(16);

        result.Status.Should().Be(409);
        result.Code.Should().Be("RETURN_LOCATION_CHANGED");
        _paddyLotBags.Should().BeEmpty();
        _inventories.Should().BeEmpty();
    }

    [Theory]
    [InlineData(20, 500000, 0, 200000, 300000, 0)]
    [InlineData(20, 0, 500000, 0, 500000, 200000)]
    [InlineData(22, 100000, 0, 100000, 0, 120000)]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_AppliesCreditOnlyToSourceOutbound(
        decimal returnQuantity,
        decimal sourceOutstanding,
        decimal otherOrderOutstanding,
        decimal expectedDebtReduction,
        decimal expectedReceivableBalance,
        decimal expectedRefundPending)
    {
        var (order, _, _) = ArrangeGoodReturnForConfirmation(30, returnQuantity, withExistingOpenBag: false);
        const int sourceOutboundId = 90;
        order.OutboundOrderId = sourceOutboundId;

        var receivable = _partyDebts.Single(x => x.Direction == LookupCodes.DebtDirection.Receivable);
        receivable.CurrentBalance = sourceOutstanding + otherOrderOutstanding;

        const decimal sourceCharge = 500000m;
        _debtTransactions.Add(new DebtTransaction
        {
            PartyDebtId = receivable.Id,
            RefType = "OUTBOUND_ORDER",
            RefId = sourceOutboundId,
            TransactionType = LookupCodes.DebtTransactionType.Charge,
            Amount = sourceCharge
        });
        if (sourceCharge > sourceOutstanding)
        {
            _debtTransactions.Add(new DebtTransaction
            {
                PartyDebtId = receivable.Id,
                RefType = "OUTBOUND_ORDER",
                RefId = sourceOutboundId,
                TransactionType = LookupCodes.DebtTransactionType.Payment,
                Amount = sourceCharge - sourceOutstanding
            });
        }
        if (otherOrderOutstanding > 0)
        {
            _debtTransactions.Add(new DebtTransaction
            {
                PartyDebtId = receivable.Id,
                RefType = "OUTBOUND_ORDER",
                RefId = 99,
                TransactionType = LookupCodes.DebtTransactionType.Charge,
                Amount = otherOrderOutstanding
            });
        }

        var result = await _sut.ConfirmAsync(order.Id);

        result.Status.Should().Be(200);
        order.DebtReductionAmount.Should().Be(expectedDebtReduction);
        order.RefundPendingAmount.Should().Be(expectedRefundPending);
        receivable.CurrentBalance.Should().Be(expectedReceivableBalance);

        var returnCredits = _debtTransactions
            .Where(x => x.DeduplicationKey == $"CRT-CONFIRM-{order.Id}")
            .ToList();
        if (expectedDebtReduction > 0)
        {
            returnCredits.Should().ContainSingle(x =>
                x.TransactionType == LookupCodes.DebtTransactionType.ReturnCredit &&
                x.Amount == expectedDebtReduction &&
                x.RefType == "OUTBOUND_ORDER" &&
                x.RefId == sourceOutboundId);
        }
        else
        {
            returnCredits.Should().BeEmpty();
        }

        if (expectedRefundPending > 0)
        {
            var payable = _partyDebts.Single(x => x.Direction == LookupCodes.DebtDirection.Payable);
            payable.CurrentBalance.Should().Be(expectedRefundPending);
            _debtTransactions.Should().ContainSingle(x =>
                x.TransactionType == LookupCodes.DebtTransactionType.RefundPayable &&
                x.Amount == expectedRefundPending &&
                x.RefId == order.Id);
        }
    }

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task ConfirmAsync_QuantityExceedsLimit_ReturnsBadRequest()
    {
        // Arrange
        var order = new CustomerReturnOrder 
        { 
            Id = 2, 
            OrganizationId = 1,
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
            ProductVariantId = 5,
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
        _locations.Add(new Location
        {
            Id = 100, WarehouseId = 1, IsActive = true, IsQuarantine = false,
            CurrentOccupancy = 0, MaxCapacity = 100m
        });

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
            OrganizationId = 1,
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
            ProductVariantId = 5,
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
        _locations.Add(new Location
        {
            Id = 100, WarehouseId = 1, IsActive = true, IsQuarantine = false,
            CurrentOccupancy = 0, MaxCapacity = 100m
        });

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

    [Fact]
    [Trait("Service", "CustomerReturnOrder")]
    public async Task RegisterRefundAsync_AmountAndOptionalNote_ReducesCustomerPayable()
    {
        var order = new CustomerReturnOrder
        {
            Id = 40,
            OrganizationId = 1,
            CustomerId = 12,
            ReturnCode = "CRT-040",
            CustomerReturnOrderStatusId = 4,
            CustomerReturnOrderStatus = _customerReturnOrderStatuses.Single(x => x.Code == CustomerReturnOrderStatusNames.Confirmed),
            ApprovedCreditAmount = 120000m,
            RefundPendingAmount = 120000m
        };
        var payable = new global::Backend.Domain.Entities.PartyDebt
        {
            Id = 401,
            OrganizationId = 1,
            PartyType = LookupCodes.PartyType.Customer,
            PartyId = 12,
            Direction = LookupCodes.DebtDirection.Payable,
            CurrentBalance = 120000m,
            IsActive = true
        };
        _customerReturnOrders.Add(order);
        _partyDebts.Add(payable);

        var result = await _sut.RegisterRefundAsync(order.Id, new RegisterCustomerReturnRefundDto
        {
            Amount = 120000m,
            Note = "Hoàn tại quầy"
        });

        result.Status.Should().Be(200);
        payable.CurrentBalance.Should().Be(0m);
        order.RefundedAmount.Should().Be(120000m);
        order.RefundPendingAmount.Should().Be(0m);
        order.RefundStatus.Should().Be(CustomerReturnRefundStatus.Refunded);
        _debtTransactions.Should().ContainSingle(x =>
            x.TransactionType == LookupCodes.DebtTransactionType.Payment &&
            x.Amount == 120000m &&
            x.DeduplicationKey == "CRT-REFUND-40-120000" &&
            x.Note == "Hoàn tiền đơn CRT-040. Hoàn tại quầy");
    }
}
