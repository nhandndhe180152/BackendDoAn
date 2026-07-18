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
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Putaway;

public class PutawaySuggestionServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ILocationRepository> _locationRepositoryMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
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

    public PutawaySuggestionServiceTests()
    {
        // Setup DbContext DbSets
        _contextMock.Setup(c => c.Locations).Returns(MockDbSet(_locations).Object);
        _contextMock.Setup(c => c.ProductVariants).Returns(MockDbSet(_productVariants).Object);
        _contextMock.Setup(c => c.SystemConfigs).Returns(MockDbSet(_systemConfigs).Object);
        _contextMock.Setup(c => c.PutawayDecisions).Returns(MockDbSet(_decisions).Object);
        _contextMock.Setup(c => c.PaddyLots).Returns(MockDbSet(_paddyLots).Object);
        _contextMock.Setup(c => c.PaddyPurchaseReceipts).Returns(MockDbSet(_paddyPurchaseReceipts).Object);
        _contextMock.Setup(c => c.InboundOrders).Returns(MockDbSet(_inboundOrders).Object);
        _contextMock.Setup(c => c.InboundOrderStatuses).Returns(MockDbSet(_inboundOrderStatuses).Object);
        _contextMock.Setup(c => c.MillingOrders).Returns(MockDbSet(_millingOrders).Object);
        _contextMock.Setup(c => c.CustomerReturnOrders).Returns(MockDbSet(_customerReturnOrders).Object);
        _contextMock.Setup(c => c.CustomerReturnOrderStatuses).Returns(MockDbSet(_customerReturnOrderStatuses).Object);
        _contextMock.Setup(c => c.StockTransfers).Returns(MockDbSet(_stockTransfers).Object);
        _contextMock.Setup(c => c.Inventories).Returns(MockDbSet(_inventories).Object);
        _contextMock.Setup(c => c.InventoryTransactions).Returns(MockDbSet(_inventoryTransactions).Object);

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

        return mockDbSet;
    }

    private PutawaySuggestionService Sut()
    {
        return new PutawaySuggestionService(
            _contextMock.Object,
            _locationRepositoryMock.Object,
            _httpContextAccessorMock.Object,
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
            WeightKg = 500
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 999, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_PaddyPurchaseAlreadyConfirmed_ReturnsConflict()
    {
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 500 });
        _decisions.Add(new PutawayDecision { ReferenceType = "PADDY_PURCHASE", ReferenceId = 10, RequiredWeightKg = 500 });
        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("đã nhập kho đủ");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_MissingOverrideReason_ReturnsUnprocessableEntity()
    {
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, IsActive = true });
        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            SuggestedLocationId = 102, // Different location, implies override!
            WeightKg = 500,
            OverrideReason = "" // missing!
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
        _productVariants.Add(new ProductVariant { Id = 2 });
        _locations.Add(new Location { Id = 101, WarehouseId = 1, IsActive = true });

        _locationRepositoryMock
            .Setup(r => r.UpdateCapacitySafetyAsync(101, 1, 500, 2, It.IsAny<bool>(), 1))
            .ReturnsAsync(0); // 0 rows affected indicates conflict!

        var request = new ConfirmStoreInRequest
        {
            ProductVariantId = 2,
            SelectedLocationId = 101,
            WeightKg = 500
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.Status.Should().Be((int)HttpStatusCode.Conflict);
        result.Message.Should().Contain("Tình trạng vị trí đã thay đổi");
    }

    [Fact]
    [Trait("Service", "Putaway")]
    public async Task ConfirmStoreInAsync_SuccessFlow_CommitTransaction()
    {
        _paddyPurchaseReceipts.Add(new PaddyPurchaseReceipt { Id = 10, WarehouseId = 1, ActualWeightKg = 1000 });
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
            WeightKg = 500
        };

        var result = await Sut().ConfirmStoreInAsync("PADDY_PURCHASE", 10, request, CancellationToken.None);

        result.IsSucceeded.Should().BeTrue();
        result.Message.Should().Contain("thành công");
    }
}
