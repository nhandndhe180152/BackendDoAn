using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Backend.Share.Services;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Putaway;

public class PutawaySuggestionServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ILocationRepository> _locationRepositoryMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<IScheduledJobService> _scheduledJobServiceMock = new();
    private readonly Mock<ILogger<PutawaySuggestionService>> _loggerMock = new();

    private readonly List<Location> _locations = new();
    private readonly List<ProductVariant> _productVariants = new();
    private readonly List<SystemConfig> _systemConfigs = new();
    private readonly List<PutawayDecision> _decisions = new();
    private readonly List<Backend.Domain.Entities.PaddyLot> _paddyLots = new();
    private readonly List<PaddyPurchaseReceipt> _paddyPurchaseReceipts = new();
    private readonly List<InboundOrder> _inboundOrders = new();
    private readonly List<InboundOrderStatus> _inboundOrderStatuses = new();
    private readonly List<Backend.Domain.Entities.MillingOrder> _millingOrders = new();
    private readonly List<CustomerReturnOrder> _customerReturnOrders = new();
    private readonly List<CustomerReturnOrderStatus> _customerReturnOrderStatuses = new();
    private readonly List<StockTransfer> _stockTransfers = new();
    private readonly List<Backend.Domain.Entities.Inventory> _inventories = new();
    private readonly List<InventoryTransaction> _inventoryTransactions = new();
    private readonly List<Backend.Domain.Entities.PaddyPurchaseSchedule> _paddyPurchaseSchedules = new();
    private readonly List<Backend.Domain.Entities.PaddyPurchaseScheduleStatus> _paddyPurchaseScheduleStatuses = new();

    public PutawaySuggestionServiceTests()
    {
        // Setup DbContext DbSets dynamically using lambda to reflect list updates
        _contextMock.Setup(c => c.Locations).Returns(() => MockDbSet(_locations).Object);
        _contextMock.Setup(c => c.ProductVariants).Returns(() => MockDbSet(_productVariants).Object);
        _contextMock.Setup(c => c.SystemConfigs).Returns(() => MockDbSet(_systemConfigs).Object);
        _contextMock.Setup(c => c.PutawayDecisions).Returns(() => MockDbSet(_decisions).Object);
        _contextMock.Setup(c => c.PaddyLots).Returns(() => MockDbSet(_paddyLots).Object);
        _contextMock.Setup(c => c.PaddyPurchaseReceipts).Returns(() => MockDbSet(_paddyPurchaseReceipts).Object);
        _contextMock.Setup(c => c.InboundOrders).Returns(() => MockDbSet(_inboundOrders).Object);
        _contextMock.Setup(c => c.InboundOrderStatuses).Returns(() => MockDbSet(_inboundOrderStatuses).Object);
        _contextMock.Setup(c => c.MillingOrders).Returns(() => MockDbSet(_millingOrders).Object);
        _contextMock.Setup(c => c.CustomerReturnOrders).Returns(() => MockDbSet(_customerReturnOrders).Object);
        _contextMock.Setup(c => c.CustomerReturnOrderStatuses).Returns(() => MockDbSet(_customerReturnOrderStatuses).Object);
        _contextMock.Setup(c => c.StockTransfers).Returns(() => MockDbSet(_stockTransfers).Object);
        _contextMock.Setup(c => c.Inventories).Returns(() => MockDbSet(_inventories).Object);
        _contextMock.Setup(c => c.InventoryTransactions).Returns(() => MockDbSet(_inventoryTransactions).Object);
        _contextMock.Setup(c => c.PaddyPurchaseSchedules).Returns(() => MockDbSet(_paddyPurchaseSchedules).Object);
        _contextMock.Setup(c => c.PaddyPurchaseScheduleStatuses).Returns(() => MockDbSet(_paddyPurchaseScheduleStatuses).Object);

        // Setup HttpContext
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        _httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        // Setup DbTransaction Mock
        var mockDbContext = new Mock<DbContext>();
        var mockDatabaseFacade = new Mock<DatabaseFacade>(mockDbContext.Object);
        var mockTransaction = new Mock<IDbContextTransaction>();
        mockDatabaseFacade
            .Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTransaction.Object);
        _contextMock.Setup(c => c.Database).Returns(mockDatabaseFacade.Object);

        // Default location safety update setup (returns 1 for success)
        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int>()))
            .ReturnsAsync(1);
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

        // Mock DbSet operations
        mockDbSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(list.Add);
        mockDbSet.Setup(d => d.Update(It.IsAny<T>()));
        mockDbSet.Setup(d => d.Remove(It.IsAny<T>())).Callback<T>(t => list.Remove(t));

        return mockDbSet;
    }

    private PutawaySuggestionService Sut()
    {
        return new PutawaySuggestionService(
            _contextMock.Object,
            _locationRepositoryMock.Object,
            _httpContextAccessorMock.Object,
            _scheduledJobServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task GetSuggestionsAsync_RequiredWeightZero_ReturnsBadRequest()
    {
        var request = new GetPutawaySuggestionsRequest(1, 2, null, 0);

        var result = await Sut().GetSuggestionsAsync(request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.BadRequest);
        result.Message.Should().Contain("lớn hơn 0");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task GetSuggestionsAsync_ProductVariantNotFound_ReturnsNotFound()
    {
        var request = new GetPutawaySuggestionsRequest(1, 99, null, 100);

        var result = await Sut().GetSuggestionsAsync(request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.NotFound);
        result.Message.Should().Contain("loại sản phẩm");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task GetSuggestionsAsync_NoConfigInDb_UsesDefaultsAndSucceeds()
    {
        _productVariants.Add(new ProductVariant { Id = 2, Product = new Domain.Entities.Product { ProductCategoryId = 10 } });
        _locations.Add(new Location
        {
            Id = 101,
            WarehouseId = 1,
            SlotCode = "LOC-101",
            ZoneName = "Zone A",
            MaxCapacity = 1000m,
            CurrentOccupancy = 200m,
            CurrentProductVariantId = 2,
            AllowedCategoryId = 10,
            Priority = 80,
            IsActive = true,
            IsQuarantine = false
        });
        var request = new GetPutawaySuggestionsRequest(1, 2, null, 100);

        var result = await Sut().GetSuggestionsAsync(request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.OK);
        var response = (PutawaySuggestionsResponse)result.Resources;
        response.HasSuggestion.Should().BeTrue();
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task GetSuggestionsAsync_ConfigWeightSumInvalid_ReturnsUnprocessableEntity()
    {
        _productVariants.Add(new ProductVariant { Id = 2, Product = new Domain.Entities.Product { ProductCategoryId = 10 } });
        
        // Seed invalid sum of weights: 0.5 + 0.5 + 0.5 = 1.5 != 1.0
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_CAPACITY_FIT_WEIGHT_KEY, ConfigValue = "0.5" });
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_OCCUPANCY_WEIGHT_KEY, ConfigValue = "0.5" });
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_CATEGORY_MATCH_WEIGHT_KEY, ConfigValue = "0.5" });
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_PRIORITY_WEIGHT_KEY, ConfigValue = "0.0" });

        var request = new GetPutawaySuggestionsRequest(1, 2, null, 100);

        var result = await Sut().GetSuggestionsAsync(request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.UnprocessableEntity);
        result.Message.Should().Contain("bằng 1");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task GetSuggestionsAsync_NormalFlowSuggestionsFound_ReturnsTopRankedSuggestions()
    {
        _productVariants.Add(new ProductVariant { Id = 2, Product = new Domain.Entities.Product { ProductCategoryId = 10 } });
        
        // Seed valid weights
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_CAPACITY_FIT_WEIGHT_KEY, ConfigValue = "0.4" });
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_OCCUPANCY_WEIGHT_KEY, ConfigValue = "0.3" });
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_CATEGORY_MATCH_WEIGHT_KEY, ConfigValue = "0.2" });
        _systemConfigs.Add(new SystemConfig { ConfigKey = CommonConstants.SystemConfig.PUTAWAY_PRIORITY_WEIGHT_KEY, ConfigValue = "0.1" });

        _locations.Add(new Location
        {
            Id = 101,
            WarehouseId = 1,
            SlotCode = "LOC-101",
            ZoneName = "Zone A",
            MaxCapacity = 1000m,
            CurrentOccupancy = 200m,
            CurrentProductVariantId = 2,
            AllowedCategoryId = 10,
            Priority = 80,
            IsActive = true,
            IsQuarantine = false
        });
        var request = new GetPutawaySuggestionsRequest(1, 2, null, 300);

        var result = await Sut().GetSuggestionsAsync(request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.OK);
        var response = (PutawaySuggestionsResponse)result.Resources;
        response.HasSuggestion.Should().BeTrue();
        response.Suggestions.Should().HaveCount(1);
        response.Suggestions[0].LocationId.Should().Be(101);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task GetSuggestionsAsync_SingleColumnCannotFit_ReturnsSplitSuggestions()
    {
        _productVariants.Add(new ProductVariant { Id = 2, Product = new Domain.Entities.Product { ProductCategoryId = 10 } });
        
        _locations.Add(new Location
        {
            Id = 101,
            WarehouseId = 1,
            MaxCapacity = 1000m,
            CurrentOccupancy = 400m,
            CurrentProductVariantId = 2,
            IsActive = true
        });
        _locations.Add(new Location
        {
            Id = 102,
            WarehouseId = 1,
            MaxCapacity = 1000m,
            CurrentOccupancy = 400m,
            CurrentProductVariantId = null,
            IsActive = true
        });
        var request = new GetPutawaySuggestionsRequest(1, 2, null, 1000);

        var result = await Sut().GetSuggestionsAsync(request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.OK);
        var response = (PutawaySuggestionsResponse)result.Resources;
        response.HasSuggestion.Should().BeFalse();
        response.CanSplit.Should().BeTrue();
        response.SplitSuggestions.Should().HaveCount(2);
        response.SplitSuggestions[0].LocationId.Should().Be(101);
        response.SplitSuggestions[0].WeightKg.Should().Be(600);
        response.SplitSuggestions[1].LocationId.Should().Be(102);
        response.SplitSuggestions[1].WeightKg.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task UpdateConfig_InvalidWeightSum_ReturnsUnprocessableEntity()
    {
        var dto = new UpdatePutawayRuleConfigDto
        {
            CapacityWeight = 0.5m,
            OccupancyWeight = 0.5m,
            CategoryWeight = 0.5m,
            PriorityWeight = 0.0m
        };

        var result = await Sut().UpdateConfigAsync(1, dto, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.UnprocessableEntity);
        result.Message.Should().Contain("bằng 1");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_DocumentNotFound_ReturnsNotFound()
    {
        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 999, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_PaddyPurchaseAlreadyConfirmed_ReturnsConflict()
    {
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 500 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 });
        _inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, LocationId = null, ProductVariantId = 2, PaddyLotId = 30, QuantityOnHand = 1000 });
        _decisions.Add(new PutawayDecision { ReferenceType = "PADDY_PURCHASE", ReferenceId = 10, RequiredWeightKg = 500 });
        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("vượt quá khối lượng còn lại");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_MissingOverrideReason_ReturnsUnprocessableEntity()
    {
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 });
        _inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, LocationId = null, ProductVariantId = 2, PaddyLotId = 30, QuantityOnHand = 1000 });
        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            SuggestedLocationId = 102, // Different location, implies override!
            WeightKg = 500,
            OverrideReason = "", // missing!
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.UnprocessableEntity);
        result.Message.Should().Contain("nhập lý do");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_UpdateCapacityClash_ReturnsConflict()
    {
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 });
        _inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, LocationId = null, ProductVariantId = 2, PaddyLotId = 30, QuantityOnHand = 1000 });
        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 500, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(0); // 0 rows affected indicates conflict!

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("Tình trạng vị trí đã thay đổi", result.Message);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_SuccessFlow_CommitTransaction()
    {
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 });
        _inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, LocationId = null, ProductVariantId = 2, PaddyLotId = 30, QuantityOnHand = 1000 });
        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 500, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1); // 1 row affected = success!

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            SuggestedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.IsSucceeded.Should().BeTrue(result.Message);
        result.Message.Should().Contain("thành công");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ChốtPhiếu_TạoTồnKhuĐệm_ChưaChuyểnLịchSTOCKED()
    {
        // Setup PaddyPurchaseReceiptService dependencies to test confirm receipt
        var receiptRepoMock = new Mock<IPaddyPurchaseReceiptRepository>();
        var paddyLotRepoMock = new Mock<IPaddyLotRepository>();
        var inboundOrderRepoMock = new Mock<IRepositoryBase<InboundOrder, int>>();
        var inboundOrderItemRepoMock = new Mock<IRepositoryBase<InboundOrderItem, int>>();
        var inboundOrderStatusRepoMock = new Mock<IRepositoryBase<InboundOrderStatus, int>>();
        var lotStatusRepoMock = new Mock<IRepositoryBase<LotStatus, int>>();
        var partyDebtRepoMock = new Mock<IRepositoryBase<Backend.Domain.Entities.PartyDebt, int>>();
        var debtTransactionRepoMock = new Mock<IRepositoryBase<Backend.Domain.Entities.DebtTransaction, int>>();
        var productVariantRepoMock = new Mock<IProductVariantRepository>();
        var scheduleRepoMock = new Mock<IPaddyPurchaseScheduleRepository>();
        var systemLookupMock = new Mock<ISystemLookup>();
        var inventoryRepoMock = new Mock<IInventoryRepository>();
        var inventoryTransactionRepoMock = new Mock<IInventoryTransactionRepository>();

        var receipt = new PaddyPurchaseReceipt
        {
            Id = 10,
            WarehouseId = 1,
            ActualWeightKg = 1000,
            ScheduleId = 100,
            TotalAmount = 10000,
            DebtAmount = 0
        };

        receiptRepoMock.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyPurchaseReceipt, bool>>>(), 
                It.IsAny<bool>()))
            .Returns(new List<PaddyPurchaseReceipt> { receipt }.AsQueryable().BuildMock());

        receiptRepoMock.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PaddyPurchaseReceipt, bool>>>(), 
                It.IsAny<bool>(), 
                It.IsAny<Expression<Func<PaddyPurchaseReceipt, object>>[]>()))
            .Returns(new List<PaddyPurchaseReceipt> { receipt }.AsQueryable().BuildMock());

        paddyLotRepoMock.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.PaddyLot, bool>>>()))
            .ReturnsAsync(false);

        paddyLotRepoMock.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.PaddyLot, bool>>>(), 
                It.IsAny<bool>()))
            .Returns(new List<Backend.Domain.Entities.PaddyLot>().AsQueryable().BuildMock());

        paddyLotRepoMock.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.PaddyLot, bool>>>(), 
                It.IsAny<bool>(), 
                It.IsAny<Expression<Func<Backend.Domain.Entities.PaddyLot, object>>[]>()))
            .Returns(new List<Backend.Domain.Entities.PaddyLot>().AsQueryable().BuildMock());

        // FirstOrDefaultAsync signature has optional arguments
        lotStatusRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<LotStatus, bool>>>(), 
                It.IsAny<bool>(), 
                It.IsAny<Expression<Func<LotStatus, object>>[]>()))
            .ReturnsAsync(new LotStatus { Id = 1, Name = "ACTIVE" });

        inboundOrderStatusRepoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<InboundOrderStatus, bool>>>(), 
                It.IsAny<bool>(), 
                It.IsAny<Expression<Func<InboundOrderStatus, object>>[]>()))
            .ReturnsAsync(new InboundOrderStatus { Id = 1, Name = InboundOrderStatusNames.Confirmed });

        productVariantRepoMock.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<ProductVariant, bool>>>(), 
                It.IsAny<bool>()))
            .Returns(new List<ProductVariant> { new ProductVariant { Id = 2 } }.AsQueryable().BuildMock());

        productVariantRepoMock.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<ProductVariant, bool>>>(), 
                It.IsAny<bool>(), 
                It.IsAny<Expression<Func<ProductVariant, object>>[]>()))
            .Returns(new List<ProductVariant> { new ProductVariant { Id = 2 } }.AsQueryable().BuildMock());

        var mockTx = new Mock<IDbContextTransaction>();
        receiptRepoMock.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        var bufferInventoryMock = new Mock<Backend.Domain.Entities.Inventory>();
        inventoryRepoMock.Setup(r => r.GetByVariantWarehouseLocationAsync(2, 1, null, It.IsAny<int?>()))
            .ReturnsAsync((Backend.Domain.Entities.Inventory)null);

        var putawayDecisionRepoMock = new Mock<IRepositoryBase<PutawayDecision, long>>();
        putawayDecisionRepoMock.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PutawayDecision, bool>>>(), 
                It.IsAny<bool>()))
            .Returns(new List<PutawayDecision>().AsQueryable().BuildMock());

        var schedule = new Backend.Domain.Entities.PaddyPurchaseSchedule { Id = 100, StatusId = 1 }; // Mới tạo (1)
        scheduleRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(schedule);

        systemLookupMock.Setup(s => s.PaddyScheduleStatusId("CANCELLED")).Returns(6);
        systemLookupMock.Setup(s => s.PaddyScheduleStatusId("STOCKED")).Returns(5);
        systemLookupMock.Setup(s => s.PaddyScheduleStatusId("PARTIALLY_STOCKED")).Returns(7);
        systemLookupMock.Setup(s => s.PaddyScheduleStatusId("WEIGHED")).Returns(4);

        var service = new PaddyPurchaseReceiptService(
            receiptRepoMock.Object,
            paddyLotRepoMock.Object,
            inboundOrderRepoMock.Object,
            inboundOrderItemRepoMock.Object,
            inboundOrderStatusRepoMock.Object,
            lotStatusRepoMock.Object,
            partyDebtRepoMock.Object,
            debtTransactionRepoMock.Object,
            productVariantRepoMock.Object,
            scheduleRepoMock.Object,
            systemLookupMock.Object,
            _httpContextAccessorMock.Object,
            inventoryRepoMock.Object,
            inventoryTransactionRepoMock.Object,
            putawayDecisionRepoMock.Object,
            new Mock<Backend.Application.Interfaces.INotificationDispatcher>().Object
        );

        var res = await service.ConfirmReceiptAsync(10, 1);

        res.IsSucceeded.Should().BeTrue();
        // Verify buffer inventory was created/saved
        inventoryRepoMock.Verify(r => r.CreateAsync(It.Is<Backend.Domain.Entities.Inventory>(i => i.LocationId == null && i.QuantityOnHand == 1000)), Times.Once);
        // Verify update schedule status was called to transition schedule status to WEIGHED (4)
        scheduleRepoMock.Verify(r => r.UpdateAsync(It.Is<Backend.Domain.Entities.PaddyPurchaseSchedule>(s => s.StatusId == 4)), Times.Once);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_ToànBộ_KhuĐệmVề0_VịTríThậtTăngĐúng_LịchSTOCKED()
    {
        var schedule = new Backend.Domain.Entities.PaddyPurchaseSchedule { Id = 100, StatusId = 4 }; // 4 = WEIGHED
        _paddyPurchaseSchedules.Add(schedule);

        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 4, Code = "WEIGHED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 5, Code = "STOCKED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 7, Code = "PARTIALLY_STOCKED" });

        var receipt = new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000, ScheduleId = 100 };
        _paddyPurchaseReceipts.Add(receipt);

        var lot = new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 };
        _paddyLots.Add(lot);

        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });

        // Buffer inventory: QuantityOnHand = 1000
        var bufferInv = new Backend.Domain.Entities.Inventory
        {
            Id = 50,
            WarehouseId = 1,
            LocationId = null,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 1000,
            CostPrice = 10
        };
        _inventories.Add(bufferInv);

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 1000, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            SuggestedLocationId = 101,
            WeightKg = 1000,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.IsSucceeded.Should().BeTrue();
        bufferInv.QuantityOnHand.Should().Be(0); // buffer is now empty

        var realInv = _inventories.FirstOrDefault(x => x.LocationId == 101);
        realInv.Should().NotBeNull();
        realInv.QuantityOnHand.Should().Be(1000);
        realInv.CostPrice.Should().Be(10); // cost price preserved

        schedule.StatusId.Should().Be(5); // STOCKED
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_MộtPhần_TổngTồnKhôngĐổi_LịchPARTIALLY_STOCKED()
    {
        var schedule = new Backend.Domain.Entities.PaddyPurchaseSchedule { Id = 100, StatusId = 4 };
        _paddyPurchaseSchedules.Add(schedule);

        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 4, Code = "WEIGHED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 5, Code = "STOCKED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 7, Code = "PARTIALLY_STOCKED" });

        var receipt = new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000, ScheduleId = 100 };
        _paddyPurchaseReceipts.Add(receipt);

        var lot = new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 };
        _paddyLots.Add(lot);

        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });

        var bufferInv = new Backend.Domain.Entities.Inventory
        {
            Id = 50,
            WarehouseId = 1,
            LocationId = null,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 1000,
            CostPrice = 10
        };
        _inventories.Add(bufferInv);

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 400, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            SuggestedLocationId = 101,
            WeightKg = 400,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.IsSucceeded.Should().BeTrue();
        bufferInv.QuantityOnHand.Should().Be(600); // 1000 - 400

        foreach (var item in _inventories)
        {
            System.Console.WriteLine($"[DEBUG] Inventory: Id={item.Id}, LocationId={item.LocationId}, Qty={item.QuantityOnHand}");
        }

        var realInv = _inventories.FirstOrDefault(x => x.LocationId == 101);
        realInv.Should().NotBeNull();
        realInv.QuantityOnHand.Should().Be(400);

        var totalQty = bufferInv.QuantityOnHand + realInv.QuantityOnHand;
        totalQty.Should().Be(1000); // total stock preserved

        schedule.StatusId.Should().Be(7); // PARTIALLY_STOCKED
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_ChiaNhiềuVịTrí()
    {
        var schedule = new Backend.Domain.Entities.PaddyPurchaseSchedule { Id = 100, StatusId = 4 };
        _paddyPurchaseSchedules.Add(schedule);

        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 4, Code = "WEIGHED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 7, Code = "PARTIALLY_STOCKED" });

        var receipt = new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000, ScheduleId = 100 };
        _paddyPurchaseReceipts.Add(receipt);

        var lot = new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 };
        _paddyLots.Add(lot);

        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _locations.Add(new Location { Id = 102, WarehouseId = 1, IsActive = true });

        var bufferInv = new Backend.Domain.Entities.Inventory
        {
            Id = 50,
            WarehouseId = 1,
            LocationId = null,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 1000,
            CostPrice = 10
        };
        _inventories.Add(bufferInv);

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 300, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);
        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(102, 1, 500, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);

        // Store 300 to Location 101
        var request1 = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            SuggestedLocationId = 101,
            WeightKg = 300,
            PaddyLotId = 30
        };
        var res1 = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request1, CancellationToken.None);
        res1.IsSucceeded.Should().BeTrue();

        // Store 500 to Location 102
        var request2 = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 102,
            SuggestedLocationId = 102,
            WeightKg = 500,
            PaddyLotId = 30
        };
        var res2 = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request2, CancellationToken.None);
        res2.IsSucceeded.Should().BeTrue();

        bufferInv.QuantityOnHand.Should().Be(200); // 1000 - 300 - 500

        var real1 = _inventories.FirstOrDefault(x => x.LocationId == 101);
        var real2 = _inventories.FirstOrDefault(x => x.LocationId == 102);

        real1.QuantityOnHand.Should().Be(300);
        real2.QuantityOnHand.Should().Be(500);

        schedule.StatusId.Should().Be(7); // PARTIALLY_STOCKED
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_ThiếuPaddyLotId_TrảLỗi()
    {
        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = null // missing lot id
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.BadRequest);
        result.Message.Should().Contain("PaddyLotId");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_PaddyLotIdKhôngThuộcPhiếu_TrảLỗi()
    {
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1 });
        // Lot belongs to receipt 99
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 99, WarehouseId = 1, ProductVariantId = 2 });

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("không thuộc về phiếu thu mua này");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_KhôngTìmThấyTồnKhuĐệm_KhôngTăngTồnVịTrí()
    {
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, WarehouseId = 1, ProductVariantId = 2 });

        // No inventory record for buffer zone

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 100,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("tồn kho đệm");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_TồnKhuĐệmKhôngĐủ_Rollback()
    {
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, WarehouseId = 1, ProductVariantId = 2 });

        // Buffer has only 200kg
        var bufferInv = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            LocationId = null,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 200
        };
        _inventories.Add(bufferInv);

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 300, // demands 300kg (insufficient)
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("không đủ để chuyển đi");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_VịTríKhácKho_TrảLỗi()
    {
        // Location belongs to Warehouse 2
        _locations.Add(new Location { Id = 101, WarehouseId = 2, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1 });
        // Lot belongs to Warehouse 1
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, WarehouseId = 1, ProductVariantId = 2 });

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 100,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("không thuộc kho hàng của lô lúa");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_ĐãNhậpĐủ_GọiLạiKhôngCộngThêm()
    {
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, WarehouseId = 1, ProductVariantId = 2 });

        // Putaway decisions recorded total 1000 kg
        _decisions.Add(new PutawayDecision { ReferenceType = "PADDY_PURCHASE", ReferenceId = 10, RequiredWeightKg = 1000 });

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 100,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("vượt quá khối lượng còn lại");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_GiáVốnChuyểnĐúng_BảoToànTổngGiáTrị()
    {
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1500 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, WarehouseId = 1, ProductVariantId = 2 });

        // Buffer has 1000kg with cost price 12.5
        var buffer = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            LocationId = null,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 1000,
            CostPrice = 12.5m
        };
        _inventories.Add(buffer);

        // Real location has 500kg with cost price 10.0
        var real = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            LocationId = 101,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 500,
            CostPrice = 10.0m
        };
        _inventories.Add(real);

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 500, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.IsSucceeded.Should().BeTrue();
        
        // Value calculations check
        // Real cost price = ((500 * 10.0) + (500 * 12.5)) / 1000 = 11.25
        real.CostPrice.Should().Be(11.25m);
        real.QuantityOnHand.Should().Be(1000);
        buffer.QuantityOnHand.Should().Be(500);

        var totalBefore = (1000 * 12.5m) + (500 * 10.0m); // 17500
        var totalAfter = (buffer.QuantityOnHand * buffer.CostPrice) + (real.QuantityOnHand * real.CostPrice); // 500 * 12.5 + 1000 * 11.25 = 6250 + 11250 = 17500
        totalAfter.Should().Be(totalBefore);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_LỗiBấtKỳBướcNào_RollbackToànBộ()
    {
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, WarehouseId = 1, ProductVariantId = 2 });

        var buffer = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            LocationId = null,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 1000
        };
        _inventories.Add(buffer);

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 500, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);

        // Intentionally mock db context to throw on SaveChanges
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated db error"));

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.IsSucceeded.Should().BeFalse();
        result.Message.Should().Contain("Lỗi xác nhận nhập kho");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_NhiềuPhiếuCùngLịch_ChỉSTOCKEDKhiTấtCảĐủ()
    {
        var schedule = new Backend.Domain.Entities.PaddyPurchaseSchedule { Id = 100, StatusId = 4 };
        _paddyPurchaseSchedules.Add(schedule);

        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 4, Code = "WEIGHED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 5, Code = "STOCKED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 7, Code = "PARTIALLY_STOCKED" });

        // Two receipts associated with schedule 100
        var r1 = new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000, ScheduleId = 100 };
        var r2 = new PaddyPurchaseReceipt { Id = 11, WarehouseId = 1, ActualWeightKg = 1000, ScheduleId = 100 };
        _paddyPurchaseReceipts.Add(r1);
        _paddyPurchaseReceipts.Add(r2);

        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, WarehouseId = 1, ProductVariantId = 2 });
        _paddyLots.Add(new Backend.Domain.Entities.PaddyLot { Id = 31, SourceReceiptId = 11, WarehouseId = 1, ProductVariantId = 2 });

        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });
        _productVariants.Add(new ProductVariant { Id = 2 });

        _inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, LocationId = null, ProductVariantId = 2, PaddyLotId = 30, QuantityOnHand = 1000 });
        _inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, LocationId = null, ProductVariantId = 2, PaddyLotId = 31, QuantityOnHand = 1000 });

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 1000, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);

        // Store-in fully for r1
        var request1 = new ConfirmStoreInRequest { ProductVariantId = 2, SelectedLocationId = 101, WeightKg = 1000, PaddyLotId = 30 };
        var res1 = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request1, CancellationToken.None);
        
        res1.IsSucceeded.Should().BeTrue();
        schedule.StatusId.Should().Be(7); // PARTIALLY_STOCKED because r2 is not yet stored!

        // Mock next save changes and decision records
        _decisions.Add(new PutawayDecision { ReferenceType = "PADDY_PURCHASE", ReferenceId = 10, RequiredWeightKg = 1000 });

        // Store-in fully for r2
        var request2 = new ConfirmStoreInRequest { ProductVariantId = 2, SelectedLocationId = 101, WeightKg = 1000, PaddyLotId = 31 };
        var res2 = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 11, request2, CancellationToken.None);

        res2.IsSucceeded.Should().BeTrue();
        schedule.StatusId.Should().Be(5); // STOCKED (both receipts completed)
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task Test_ConfirmStoreIn_LịchCANCELLED_KhôngThayĐổiTrạngThái()
    {
        var schedule = new Backend.Domain.Entities.PaddyPurchaseSchedule { Id = 100, StatusId = 6 }; // 6 = CANCELLED
        _paddyPurchaseSchedules.Add(schedule);

        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 6, Code = "CANCELLED" });
        _paddyPurchaseScheduleStatuses.Add(new Backend.Domain.Entities.PaddyPurchaseScheduleStatus { Id = 5, Code = "STOCKED" });

        var receipt = new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000, ScheduleId = 100 };
        _paddyPurchaseReceipts.Add(receipt);

        var lot = new Backend.Domain.Entities.PaddyLot { Id = 30, SourceReceiptId = 10, ProductVariantId = 2, WarehouseId = 1 };
        _paddyLots.Add(lot);

        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });

        var bufferInv = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            LocationId = null,
            ProductVariantId = 2,
            PaddyLotId = 30,
            QuantityOnHand = 1000
        };
        _inventories.Add(bufferInv);

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 1000, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(1);

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 1000,
            PaddyLotId = 30
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.IsSucceeded.Should().BeTrue();
        schedule.StatusId.Should().Be(6); // kept CANCELLED, not modified
    }
}
