using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.LowStock;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.LowStock;

public class LowStockDetectionServiceTests
{
    private readonly Mock<INotificationDispatcher> _dispatcherMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<LowStockDetectionService>> _loggerMock = new();
    private readonly Mock<ILogger<LowStockQueryService>> _queryLoggerMock = new();

    public LowStockDetectionServiceTests()
    {
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("LowStockDetectionService"))))
            .Returns(_loggerMock.Object);
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("LowStockQueryService"))))
            .Returns(_queryLoggerMock.Object);
    }

    private BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BackendContext(options);
    }

    private async Task SeedBaseDataAsync(BackendContext context)
    {
        // Seed Warehouse
        var warehouse = new Backend.Domain.Entities.Warehouse
        {
            Id = 1,
            Code = "WH-01",
            Name = "Kho chính",
            IsActive = true,
            IsDeleted = false
        };
        context.Warehouses.Add(warehouse);

        // Seed Product
        var product = new Backend.Domain.Entities.Product
        {
            Id = 1,
            Name = "Gạo ST25 10kg",
            IsDeleted = false
        };
        context.Products.Add(product);

        // Seed ProductVariant
        var variant = new ProductVariant
        {
            Id = 1,
            ProductId = 1,
            UnitOfMeasureId = 1,
            SKU = "PV-ST25",
            Name = "PV-ST25",
            CostPrice = 20000m,
            SalePrice = 25000m,
            Weight = 10m,
            IsActive = true,
            MinStockLevel = 100m, // Ngưỡng mặc định
            IsDeleted = false
        };
        context.ProductVariants.Add(variant);

        // Seed LotStatus
        var sellableStatus = new LotStatus { Id = 1, Name = "Được phép bán", Color = "#10B981", IsSellable = true };
        var quarantineStatus = new LotStatus { Id = 2, Name = "Cách ly", Color = "#EF4444", IsSellable = false };
        context.LotStatuses.AddRange(sellableStatus, quarantineStatus);

        // Seed Locations
        var normalLocation = new Location { Id = 1, WarehouseId = 1, ZoneName = "Zone A", IsActive = true, IsQuarantine = false, IsDeleted = false, CurrentOccupancy = 0 };
        var quarantineLocation = new Location { Id = 2, WarehouseId = 1, ZoneName = "Zone B", IsActive = true, IsQuarantine = true, IsDeleted = false, CurrentOccupancy = 0 };
        context.Locations.AddRange(normalLocation, quarantineLocation);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenAvailableStockIsGreaterThanThreshold_ShouldNotCreateAlert()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Inventory:OnHand=120, Reserved=10 => Available=110 > Threshold=100
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 120m,
            QuantityReserved = 10m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(0);
        var alerts = await context.Alerts.ToListAsync();
        alerts.Should().BeEmpty();
    }

    [Theory]
    [InlineData(100, 0, AlertConstants.Severity.Info)] // Available = 100 <= Threshold=100 -> INFO
    [InlineData(40, 0, AlertConstants.Severity.Warning)] // Available = 40 <= Threshold*0.5=50 -> WARNING
    [InlineData(0, 0, AlertConstants.Severity.Critical)] // Available = 0 -> CRITICAL
    public async Task DetectLowStockAsync_WhenStockIsLow_ShouldCreateAlertWithCorrectSeverity(decimal onHand, decimal reserved, string expectedSeverity)
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = onHand,
            QuantityReserved = reserved,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(1);
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.Severity.Should().Be(expectedSeverity);
        alert.Status.Should().Be(AlertConstants.Status.Open);
        alert.AlertType.Should().Be(AlertConstants.Type.LowStock);

        // Verify notification dispatched
        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.LowStockAlert,
            It.Is<NotificationTarget>(t => t.RoleIds.Contains(CommonConstants.Role.ADMIN)),
            It.IsAny<object[]>(),
            It.IsAny<string>(),
            It.IsAny<int?>()
        ), Times.Once);
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenInventoryIsInQuarantineLocation_ShouldNotCountInAvailableStock()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Inventory normal: 20 kg
        // Inventory quarantined: 100 kg (Location 2 is quarantine)
        context.Inventories.AddRange(
            new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 1, QuantityOnHand = 20m, QuantityReserved = 0m, IsDeleted = false },
            new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 2, QuantityOnHand = 100m, QuantityReserved = 0m, IsDeleted = false }
        );
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        // Available stock is only 20 kg (normal), which is <= Threshold 100 => Alert created
        result.Created.Should().Be(1);
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.Message.Should().Contain("chỉ còn 20 kg khả dụng");
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenInventoryInNonSellableLot_ShouldNotCountInAvailableStock()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Seed a non-sellable lot (StatusId = 2 is quarantine)
        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 10,
            LotCode = "LOT-001",
            LotType = "PADDY",
            ProductVariantId = 1,
            StatusId = 2, // Non-sellable
            WarehouseId = 1,
            InboundDate = DateTime.UtcNow,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        context.Inventories.AddRange(
            new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 1, PaddyLotId = null, QuantityOnHand = 30m, QuantityReserved = 0m, IsDeleted = false },
            new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 1, PaddyLotId = 10, QuantityOnHand = 150m, QuantityReserved = 0m, IsDeleted = false }
        );
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        // Available stock is only 30 kg, which is <= Threshold 100 => Alert created
        result.Created.Should().Be(1);
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.Message.Should().Contain("chỉ còn 30 kg khả dụng");
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenStockAlertConfigExists_ShouldPrioritizeConfigThreshold()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Config Threshold = 50 (ProductVariant default is 100)
        var config = new StockAlertConfig
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            MinThreshold = 50,
            IsActive = true,
            IsDeleted = false
        };
        context.StockAlertConfigs.Add(config);

        // Available = 60 kg (which is > Config Threshold 50, but < Default Threshold 100)
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 60m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        // Available = 60 > Threshold = 50 => No alert should be created
        result.Created.Should().Be(0);
        var alerts = await context.Alerts.ToListAsync();
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenRunMultipleTimesWithoutSeverityChange_ShouldNotSendDuplicateNotifications()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Stock = 80 <= Threshold = 100 -> INFO
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 80m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Run 1
        var result1 = await service.DetectLowStockAsync(CancellationToken.None);
        result1.Created.Should().Be(1);

        // Reset Mock
        _dispatcherMock.Invocations.Clear();

        // Run 2
        var result2 = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result2.Created.Should().Be(0);
        result2.Updated.Should().Be(0); // Không thay đổi số liệu/severity
        _dispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<string>(),
            It.IsAny<NotificationTarget>(),
            It.IsAny<object[]>(),
            It.IsAny<string>(),
            It.IsAny<int?>()
        ), Times.Never);
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenSeverityIncreases_ShouldUpdateAlertAndSendNewNotification()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Start with stock = 80 (INFO)
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 80m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Run 1 (creates INFO alert)
        await service.DetectLowStockAsync(CancellationToken.None);

        // Now drop stock to 0 (CRITICAL)
        inventory.QuantityOnHand = 0m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        // Reset Mock to check new notification
        _dispatcherMock.Invocations.Clear();

        // Run 2
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Updated.Should().Be(1); // Alert updated
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.Severity.Should().Be(AlertConstants.Severity.Critical);

        // Verify notification dispatched because severity increased
        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.LowStockAlert,
            It.Is<NotificationTarget>(t => t.RoleIds.Contains(CommonConstants.Role.ADMIN)),
            It.IsAny<object[]>(),
            It.IsAny<string>(),
            It.IsAny<int?>()
        ), Times.Once);
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenStockRecovers_ShouldResolveAlert()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Start low: stock = 20 <= 100
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 20m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Run 1 (Creates alert)
        await service.DetectLowStockAsync(CancellationToken.None);
        var alertBefore = await context.Alerts.FirstOrDefaultAsync();
        alertBefore!.Status.Should().Be(AlertConstants.Status.Open);

        // Stock recovers: 150 kg
        inventory.QuantityOnHand = 150m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        // Run 2
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Resolved.Should().Be(1);
        var alertAfter = await context.Alerts.FirstOrDefaultAsync();
        alertAfter!.Status.Should().Be(AlertConstants.Status.Resolved);
        alertAfter.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenDeduplicationKeyUniqueConstraintFires_ShouldFallBackToUpdate()
    {
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        var preAlert = new Alert
        {
            AlertType = AlertConstants.Type.LowStock,
            Severity = AlertConstants.Severity.Info,
            WarehouseId = 1,
            ProductVariantId = 1,
            RelatedEntityType = "PRODUCT_VARIANT",
            RelatedEntityId = 1,
            Status = AlertConstants.Status.Open,
            Message = "Cảnh báo cũ",
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false,
            DeduplicationKey = "LOW_STOCK:1:1"
        };
        context.Alerts.Add(preAlert);

        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 20m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(0);
        result.Updated.Should().Be(1);

        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.DeduplicationKey == "LOW_STOCK:1:1");
        alert.Should().NotBeNull();
        alert!.Severity.Should().Be(AlertConstants.Severity.Warning);
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenAlertIsResolved_DeduplicationKeyShouldBeNullAndAllowNewAlert()
    {
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 20m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        await service.DetectLowStockAsync(CancellationToken.None);
        var alert1 = await context.Alerts.FirstOrDefaultAsync();
        alert1.Should().NotBeNull();
        alert1!.DeduplicationKey.Should().Be("LOW_STOCK:1:1");
        alert1.Status.Should().Be(AlertConstants.Status.Open);

        inventory.QuantityOnHand = 150m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        await service.DetectLowStockAsync(CancellationToken.None);
        var alert2 = await context.Alerts.FirstOrDefaultAsync(a => a.Id == alert1.Id);
        alert2!.Status.Should().Be(AlertConstants.Status.Resolved);
        alert2.DeduplicationKey.Should().BeNull();

        inventory.QuantityOnHand = 10m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        var result = await service.DetectLowStockAsync(CancellationToken.None);
        result.Created.Should().Be(1);

        var allAlerts = await context.Alerts.ToListAsync();
        allAlerts.Count.Should().Be(2);
        var newAlert = allAlerts.FirstOrDefault(a => a.Status == AlertConstants.Status.Open);
        newAlert.Should().NotBeNull();
        newAlert!.DeduplicationKey.Should().Be("LOW_STOCK:1:1");
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenNoThreshold_ShouldIncrementSkippedCount()
    {
        // Arrange
        using var context = CreateContext();
        var warehouse = new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH-01", Name = "Kho 1", IsActive = true };
        context.Warehouses.Add(warehouse);
        var product = new Backend.Domain.Entities.Product { Id = 1, Name = "Sản phẩm", IsDeleted = false };
        context.Products.Add(product);
        var variant = new ProductVariant
        {
            Id = 1,
            ProductId = 1,
            UnitOfMeasureId = 1,
            SKU = "PV-01",
            Name = "PV-01",
            IsActive = true,
            MinStockLevel = null, // No threshold configuration
            IsDeleted = false
        };
        context.ProductVariants.Add(variant);
        context.Inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = null, QuantityOnHand = 100m, IsDeleted = false });
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task DetectLowStockAsync_ShouldNotifyBothAdminAndExecutive()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        context.Inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 1, QuantityOnHand = 10m, IsDeleted = false });
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.LowStockAlert,
            It.Is<NotificationTarget>(t => t.RoleIds.Contains(CommonConstants.Role.ADMIN) && t.RoleIds.Contains(CommonConstants.Role.EXECUTIVE)),
            It.IsAny<object[]>(),
            It.IsAny<string>(),
            It.IsAny<int?>()
        ), Times.Once);
    }
}
