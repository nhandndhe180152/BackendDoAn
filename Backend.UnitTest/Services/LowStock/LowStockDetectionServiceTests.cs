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
using Backend.Share.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.LowStock;

public class LowStockDetectionServiceTests
{
    private readonly Mock<INotificationDispatcher> _dispatcherMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<LowStockDetectionService>> _loggerMock = new();
    private readonly Mock<ILogger<LowStockQueryService>> _queryLoggerMock = new();
    
    private static readonly InMemoryDatabaseRoot _databaseRoot = new InMemoryDatabaseRoot();

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

    private FakeBackendContext CreateFakeContext(string duplicateKey, string dbName)
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: dbName, databaseRoot: _databaseRoot)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FakeBackendContext(options, duplicateKey, dbName);
    }

    private class FakeBackendContext : BackendContext
    {
        public bool SimulateConcurrency { get; set; } = false;
        private int _saveCount = 0;
        private readonly string _duplicateKey;
        private readonly string _dbName;

        public FakeBackendContext(DbContextOptions<BackendContext> options, string duplicateKey, string dbName) : base(options)
        {
            _duplicateKey = duplicateKey;
            _dbName = dbName;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (SimulateConcurrency)
            {
                _saveCount++;
                if (_saveCount == 1)
                {
                    // Concurrency simulation: insert duplicate alert under a separate context sharing the same database root
                    var options = new DbContextOptionsBuilder<BackendContext>()
                        .UseInMemoryDatabase(databaseName: _dbName, databaseRoot: _databaseRoot)
                        .Options;

                    using (var context2 = new BackendContext(options))
                    {
                        var duplicateAlert = new Alert
                        {
                            AlertType = AlertConstants.Type.LowStock,
                            Severity = AlertConstants.Severity.Info,
                            WarehouseId = 1,
                            ProductVariantId = 1,
                            RelatedEntityType = "PRODUCT_VARIANT",
                            RelatedEntityId = 1,
                            Status = AlertConstants.Status.Open,
                            Message = "Concurrency Alert",
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false,
                            DeduplicationKey = _duplicateKey
                        };
                        context2.Alerts.Add(duplicateAlert);
                        await context2.SaveChangesAsync(cancellationToken);
                    }

                    // Throw custom DbUpdateException to invoke unique check
                    throw new DbUpdateException("Unique constraint failed", new Exception("UX_Alert_DeduplicationKey"));
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
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
            MinStockLevel = 100m, // Default Threshold
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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(0);
        var alerts = await context.Alerts.ToListAsync();
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenSkuHasNoInventoryRows_ShouldCreateCriticalAlert()
    {
        // Arrange — E1 Fix regression: SKU có ngưỡng nhưng KHÔNG có dòng Inventory nào
        using var context = CreateContext();
        await SeedBaseDataAsync(context);
        // KHÔNG thêm bất kỳ dòng Inventory nào → tồn kho = 0

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert — phải sinh CRITICAL vì AvailableKg = 0 <= Threshold = 100
        result.Created.Should().Be(1);
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.Severity.Should().Be(AlertConstants.Severity.Critical);
        alert.AlertType.Should().Be(AlertConstants.Type.LowStock);
        alert.Status.Should().Be(AlertConstants.Status.Open);
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenDuplicateStockAlertConfigExists_ShouldNotCrashJob()
    {
        // Arrange — E2 Fix regression: 2 config active cùng (WarehouseId, ProductVariantId)
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // 2 config trùng nhau — trước đây sẽ crash ToDictionaryAsync
        context.StockAlertConfigs.AddRange(
            new StockAlertConfig { WarehouseId = 1, ProductVariantId = 1, MinThreshold = 80, IsActive = true, IsDeleted = false },
            new StockAlertConfig { WarehouseId = 1, ProductVariantId = 1, MinThreshold = 60, IsActive = true, IsDeleted = false }
        );
        context.Inventories.Add(new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1, ProductVariantId = 1, LocationId = 1, QuantityOnHand = 50m, IsDeleted = false
        });
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act — không được ném exception
        var act = async () => await service.DetectLowStockAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Assert — sử dụng ngưỡng thấp nhất (60), tồn 50 < 60 * 0.5 = 30 → INFO (50 > 30) → Warning (50 <= 60)
        var result = await service.DetectLowStockAsync(CancellationToken.None);
        result.Failed.Should().Be(0);
    }

    [Theory]
    [InlineData(100, 0, AlertConstants.Severity.Info)]
    [InlineData(40, 0, AlertConstants.Severity.Warning)]
    [InlineData(0, 0, AlertConstants.Severity.Critical)]
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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(1);
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.Severity.Should().Be(expectedSeverity);
        alert.Status.Should().Be(AlertConstants.Status.Open);
        alert.AlertType.Should().Be(AlertConstants.Type.LowStock);

        // Verify notification args (D5 Fix)
        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.LowStockAlert,
            It.Is<NotificationTarget>(t => t.RoleIds.Contains(CommonConstants.Role.ADMIN) && t.RoleIds.Contains(CommonConstants.Role.EXECUTIVE)),
            It.Is<object[]>(args => 
                args.Length == 4 && 
                args[0].ToString() == "PV-ST25 - Gạo ST25 10kg" && 
                args[1].ToString() == "WH-01" && 
                args[2].ToString() == $"{onHand - reserved} kg" &&
                args[3].ToString() == "100 kg"
            ),
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

        context.Inventories.AddRange(
            new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 1, QuantityOnHand = 20m, QuantityReserved = 0m, IsDeleted = false },
            new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 2, QuantityOnHand = 100m, QuantityReserved = 0m, IsDeleted = false }
        );
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
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

        var config = new StockAlertConfig
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            MinThreshold = 50,
            IsActive = true,
            IsDeleted = false
        };
        context.StockAlertConfigs.Add(config);

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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(0);
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenRunMultipleTimesWithoutSeverityChange_ShouldNotSendDuplicateNotifications()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Run 1
        var result1 = await service.DetectLowStockAsync(CancellationToken.None);
        result1.Created.Should().Be(1);

        // Reset Mock
        _dispatcherMock.Invocations.Clear();

        // Run 2
        var result2 = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result2.Created.Should().Be(0);
        result2.Updated.Should().Be(0);
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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Run 1
        await service.DetectLowStockAsync(CancellationToken.None);

        // Drop stock to 0
        inventory.QuantityOnHand = 0m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        _dispatcherMock.Invocations.Clear();

        // Run 2
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Updated.Should().Be(1);
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert!.Severity.Should().Be(AlertConstants.Severity.Critical);

        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.LowStockAlert,
            It.Is<NotificationTarget>(t => t.RoleIds.Contains(CommonConstants.Role.ADMIN) && t.RoleIds.Contains(CommonConstants.Role.EXECUTIVE)),
            It.Is<object[]>(args => 
                args.Length == 4 && 
                args[0].ToString() == "PV-ST25 - Gạo ST25 10kg" && 
                args[2].ToString() == "0 kg"
            ),
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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        await service.DetectLowStockAsync(CancellationToken.None);

        // Stock recovers
        inventory.QuantityOnHand = 150m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Resolved.Should().Be(1);
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert!.Status.Should().Be(AlertConstants.Status.Resolved);
        alert.DeduplicationKey.Should().BeNull();
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenDeduplicationKeyUniqueConstraintFires_ShouldFallBackToUpdate()
    {
        // Arrange (D1 & D3 Fix: Concurrency unique constraint via FakeBackendContext sharing same Database Root)
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateFakeContext("LOW_STOCK:1:1", dbName);
        await SeedBaseDataAsync(context);

        // Prepare inventory that triggers low stock
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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // ACTIVATE CONCURRENCY SIMULATION AFTER SEEDING DATA
        context.SimulateConcurrency = true;

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        // Should catch constraint violation and route it to Update!
        result.Created.Should().Be(0);
        result.Updated.Should().Be(1);

        var alerts = await context.Alerts.Where(a => a.DeduplicationKey == "LOW_STOCK:1:1").ToListAsync();
        alerts.Count.Should().Be(1);
        alerts.First().Message.Should().Contain("20 kg khả dụng");
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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        await service.DetectLowStockAsync(CancellationToken.None);

        inventory.QuantityOnHand = 150m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        await service.DetectLowStockAsync(CancellationToken.None);

        inventory.QuantityOnHand = 10m;
        context.Inventories.Update(inventory);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(1);
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
            MinStockLevel = null,
            IsDeleted = false
        };
        context.ProductVariants.Add(variant);
        context.Inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = null, QuantityOnHand = 100m, IsDeleted = false });
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

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
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

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

    // =========================================================================
    // D2 Fix: Unit Tests for DetectLowStockForProductAsync (Per-Product Event Path)
    // =========================================================================

    [Fact]
    public async Task DetectLowStockForProductAsync_WhenStockIsLow_ShouldCreateAlert()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        context.Inventories.Add(new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 0m, // 0 kg -> Critical severity (<= 0m)
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        await service.DetectLowStockForProductAsync(1, 1, CancellationToken.None);

        // Assert
        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.WarehouseId.Should().Be(1);
        alert.ProductVariantId.Should().Be(1);
        alert.Status.Should().Be(AlertConstants.Status.Open);
        alert.Severity.Should().Be(AlertConstants.Severity.Critical);

        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.LowStockAlert,
            It.Is<NotificationTarget>(t => t.RoleIds.Contains(CommonConstants.Role.ADMIN) && t.RoleIds.Contains(CommonConstants.Role.EXECUTIVE)),
            It.Is<object[]>(args => args.Length == 4 && args[0].ToString() == "PV-ST25 - Gạo ST25 10kg"),
            It.IsAny<string>(),
            It.IsAny<int?>()
        ), Times.Once);
    }

    [Fact]
    public async Task DetectLowStockForProductAsync_WhenStockRecovers_ShouldResolveAlert()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        var preAlert = new Alert
        {
            AlertType = AlertConstants.Type.LowStock,
            Severity = AlertConstants.Severity.Warning,
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

        // Stock recovered: 120 > 100
        context.Inventories.Add(new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 120m,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // Act
        await service.DetectLowStockForProductAsync(1, 1, CancellationToken.None);

        // Assert
        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.Id == preAlert.Id);
        alert!.Status.Should().Be(AlertConstants.Status.Resolved);
        alert.DeduplicationKey.Should().BeNull();
        alert.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectLowStockForProductAsync_WhenUniqueConstraintFires_ShouldFallBackToUpdate()
    {
        // Arrange (Concurrency violation simulation using FakeContext)
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateFakeContext("LOW_STOCK:1:1", dbName);
        await SeedBaseDataAsync(context);

        context.Inventories.Add(new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            QuantityOnHand = 30m,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _cacheMock.Object, _loggerFactoryMock.Object);

        // ACTIVATE CONCURRENCY SIMULATION AFTER SEEDING DATA
        context.SimulateConcurrency = true;

        // Act
        await service.DetectLowStockForProductAsync(1, 1, CancellationToken.None);

        // Assert
        var alerts = await context.Alerts.Where(a => a.DeduplicationKey == "LOW_STOCK:1:1").ToListAsync();
        alerts.Count.Should().Be(1); // Concurrency check handles duplicates and merges updates!
        alerts.First().Message.Should().Contain("30 kg khả dụng");
    }
}
