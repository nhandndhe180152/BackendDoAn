using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.LotQualityRecheck;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Infrastructure.Persistence;
using Backend.Share.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Alert = Backend.Domain.Entities.Alert;

namespace Backend.UnitTest.Services.LotQualityRecheck;

public class LotQualityRecheckTests
{
    private readonly Mock<INotificationDispatcher> _dispatcherMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IDataChangeNotifier> _realtimeMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<LotQualityRecheckService>> _loggerMock = new();

    public LotQualityRecheckTests()
    {
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
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
            Name = "Kho Chinh",
            IsActive = true,
            IsDeleted = false
        };
        context.Warehouses.Add(warehouse);

        // Seed LotStatus
        var pendingStatus = new Backend.Domain.Entities.LotStatus { Id = 1, Code = "PENDING_INBOUND", Name = "Cho nhap", Color = "#6B7280", IsDeleted = false };
        var inStockStatus = new Backend.Domain.Entities.LotStatus { Id = 2, Code = "IN_STOCK", Name = "Dang luu kho", Color = "#10B981", IsDeleted = false };
        var quarantineStatus = new Backend.Domain.Entities.LotStatus { Id = 4, Code = "QUARANTINE", Name = "Cach ly", Color = "#EF4444", IsDeleted = false };
        var depletedStatus = new Backend.Domain.Entities.LotStatus { Id = 6, Code = "DEPLETED", Name = "Da dung het", Color = "#9CA3AF", IsDeleted = false };

        context.LotStatuses.AddRange(pendingStatus, inStockStatus, quarantineStatus, depletedStatus);

        // Seed default configs
        var configs = new List<Backend.Domain.Entities.SystemConfig>
        {
            new() { Id = 1, ConfigKey = LotQualityRecheckConstants.ConfigKey.IntervalDays, ConfigValue = "30", Name = "IntervalDays" },
            new() { Id = 2, ConfigKey = LotQualityRecheckConstants.ConfigKey.OverdueCriticalDays, ConfigValue = "7", Name = "OverdueCriticalDays" },
            new() { Id = 3, ConfigKey = LotQualityRecheckConstants.ConfigKey.MoistureWarning, ConfigValue = "14.5", Name = "MoistureWarning" },
            new() { Id = 4, ConfigKey = LotQualityRecheckConstants.ConfigKey.MoistureCritical, ConfigValue = "16.0", Name = "MoistureCritical" },
            new() { Id = 5, ConfigKey = LotQualityRecheckConstants.ConfigKey.MoldWarning, ConfigValue = "NHE", Name = "MoldWarning" },
            new() { Id = 6, ConfigKey = LotQualityRecheckConstants.ConfigKey.MoldCritical, ConfigValue = "NANG", Name = "MoldCritical" },
            new() { Id = 7, ConfigKey = LotQualityRecheckConstants.ConfigKey.LongStoredWarning, ConfigValue = "30", Name = "LongStoredWarning" },
            new() { Id = 8, ConfigKey = LotQualityRecheckConstants.ConfigKey.LongStoredCritical, ConfigValue = "60", Name = "LongStoredCritical" }
        };
        context.SystemConfigs.AddRange(configs);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task EvaluateAllLotsAsync_NoInspections_CreatesInspectionDueAndLongStoredAlerts()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Create a lot stored for 40 days, never inspected
        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 1,
            LotCode = "LOT-001",
            LotType = "PADDY",
            WarehouseId = 1,
            StatusId = 2, // IN_STOCK
            InboundDate = DateTime.UtcNow.AddDays(-40),
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);
        await context.SaveChangesAsync();

        var service = new LotQualityRecheckService(
            context,
            new LotQualityRecheckRulesEngine(),
            _cacheMock.Object,
            _dispatcherMock.Object,
            _realtimeMock.Object,
            _loggerFactoryMock.Object);

        // Act
        var result = await service.EvaluateAllLotsAsync(CancellationToken.None);

        // Assert
        result.Processed.Should().Be(1);
        result.AlertsCreated.Should().Be(2); // Inspection Due + Long Stored

        var alerts = await context.Alerts.ToListAsync();
        alerts.Should().HaveCount(2);

        var dueAlert = alerts.Single(a => a.AlertType == LotQualityRecheckConstants.AlertType.InspectionDue);
        dueAlert.Severity.Should().Be(AlertConstants.Severity.Critical);
        dueAlert.Status.Should().Be(AlertConstants.Status.Open);

        var storedAlert = alerts.Single(a => a.AlertType == LotQualityRecheckConstants.AlertType.LongStoredLot);
        storedAlert.Severity.Should().Be(AlertConstants.Severity.Warning);
        storedAlert.Status.Should().Be(AlertConstants.Status.Open);
    }

    [Fact]
    public async Task EvaluateAllLotsAsync_MoistureExceeded_CreatesHighMoistureAlert()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 1,
            LotCode = "LOT-001",
            LotType = "PADDY",
            WarehouseId = 1,
            StatusId = 2, // IN_STOCK
            InboundDate = DateTime.UtcNow.AddDays(-10),
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        // Moisture 15.5m (above 14.5 warning threshold)
        var inspection = new Backend.Domain.Entities.QualityInspection
        {
            Id = 1,
            PaddyLotId = 1,
            InspectedAt = DateTime.UtcNow.AddDays(-2),
            MoisturePercent = 15.5m,
            IsDeleted = false
        };
        context.QualityInspections.Add(inspection);
        await context.SaveChangesAsync();

        var service = new LotQualityRecheckService(
            context,
            new LotQualityRecheckRulesEngine(),
            _cacheMock.Object,
            _dispatcherMock.Object,
            _realtimeMock.Object,
            _loggerFactoryMock.Object);

        // Act
        var result = await service.EvaluateAllLotsAsync(CancellationToken.None);

        // Assert
        result.Processed.Should().Be(1);
        result.AlertsCreated.Should().Be(1); // High Moisture Alert

        var alert = await context.Alerts.SingleAsync();
        alert.AlertType.Should().Be(LotQualityRecheckConstants.AlertType.HighMoisture);
        alert.Severity.Should().Be(AlertConstants.Severity.Warning);
    }

    [Fact]
    public async Task EvaluateAllLotsAsync_MoistureCriticalExceeded_CreatesCriticalMoistureAlert()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 1,
            LotCode = "LOT-001",
            LotType = "PADDY",
            WarehouseId = 1,
            StatusId = 2, // IN_STOCK
            InboundDate = DateTime.UtcNow.AddDays(-10),
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        // Moisture 17.0m (above 16.0 critical threshold)
        var inspection = new Backend.Domain.Entities.QualityInspection
        {
            Id = 1,
            PaddyLotId = 1,
            InspectedAt = DateTime.UtcNow.AddDays(-2),
            MoisturePercent = 17.0m,
            IsDeleted = false
        };
        context.QualityInspections.Add(inspection);
        await context.SaveChangesAsync();

        var service = new LotQualityRecheckService(
            context,
            new LotQualityRecheckRulesEngine(),
            _cacheMock.Object,
            _dispatcherMock.Object,
            _realtimeMock.Object,
            _loggerFactoryMock.Object);

        // Act
        var result = await service.EvaluateAllLotsAsync(CancellationToken.None);

        // Assert
        result.AlertsCreated.Should().Be(1);

        var alert = await context.Alerts.SingleAsync();
        alert.Severity.Should().Be(AlertConstants.Severity.Critical);
    }

    [Fact]
    public async Task EvaluateAllLotsAsync_MouldRisk_CreatesMouldAlert()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 1,
            LotCode = "LOT-001",
            LotType = "PADDY",
            WarehouseId = 1,
            StatusId = 2, // IN_STOCK
            InboundDate = DateTime.UtcNow.AddDays(-10),
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        var inspection = new Backend.Domain.Entities.QualityInspection
        {
            Id = 1,
            PaddyLotId = 1,
            InspectedAt = DateTime.UtcNow.AddDays(-2),
            MoldLevel = "NẶNG", // matches standard ordinal NANG = 2
            IsDeleted = false
        };
        context.QualityInspections.Add(inspection);
        await context.SaveChangesAsync();

        var service = new LotQualityRecheckService(
            context,
            new LotQualityRecheckRulesEngine(),
            _cacheMock.Object,
            _dispatcherMock.Object,
            _realtimeMock.Object,
            _loggerFactoryMock.Object);

        // Act
        var result = await service.EvaluateAllLotsAsync(CancellationToken.None);

        // Assert
        result.AlertsCreated.Should().Be(1);

        var alert = await context.Alerts.SingleAsync();
        alert.AlertType.Should().Be(LotQualityRecheckConstants.AlertType.MouldRisk);
        alert.Severity.Should().Be(AlertConstants.Severity.Critical); // NHE = Warning (1), NANG = Critical (2)
    }

    [Fact]
    public async Task EvaluateAllLotsAsync_QuarantinedLot_SuppressesQualityAlertsAndResolvesExisting()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Lot is quarantined (StatusId = 4)
        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 1,
            LotCode = "LOT-001",
            LotType = "PADDY",
            WarehouseId = 1,
            StatusId = 4, // QUARANTINE
            InboundDate = DateTime.UtcNow.AddDays(-40),
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        // High moisture inspection
        var inspection = new Backend.Domain.Entities.QualityInspection
        {
            Id = 1,
            PaddyLotId = 1,
            InspectedAt = DateTime.UtcNow.AddDays(-2),
            MoisturePercent = 17.5m,
            IsDeleted = false
        };
        context.QualityInspections.Add(inspection);

        // Existing high moisture alert
        var existingAlert = new Alert
        {
            Id = 101,
            AlertType = LotQualityRecheckConstants.AlertType.HighMoisture,
            Severity = AlertConstants.Severity.Critical,
            WarehouseId = 1,
            Status = AlertConstants.Status.Open,
            Message = "High moisture",
            RelatedEntityType = "PADDY_LOT",
            RelatedEntityId = 1,
            DeduplicationKey = $"{LotQualityRecheckConstants.Job.DeduplicationKeyPrefix}:PADDY_LOT:1:{LotQualityRecheckConstants.AlertType.HighMoisture}",
            IsDeleted = false
        };
        context.Alerts.Add(existingAlert);
        await context.SaveChangesAsync();

        var service = new LotQualityRecheckService(
            context,
            new LotQualityRecheckRulesEngine(),
            _cacheMock.Object,
            _dispatcherMock.Object,
            _realtimeMock.Object,
            _loggerFactoryMock.Object);

        // Act
        var result = await service.EvaluateAllLotsAsync(CancellationToken.None);

        // Assert
        result.AlertsCreated.Should().Be(1); // Long Stored is created, Inspection Due is not due yet, moisture/mould are suppressed
        result.AlertsResolved.Should().Be(1); // High Moisture is auto-resolved because lot is quarantined

        var moistureAlert = await context.Alerts.SingleAsync(a => a.AlertType == LotQualityRecheckConstants.AlertType.HighMoisture);
        moistureAlert.Status.Should().Be(AlertConstants.Status.Resolved);
        moistureAlert.DeduplicationKey.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAllLotsAsync_InvalidConfig_CreatesConfigAlertAndEvaluatesValidRules()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Update moisture configs to be invalid: moisture critical < moisture warning
        var warningConfig = await context.SystemConfigs.SingleAsync(c => c.ConfigKey == LotQualityRecheckConstants.ConfigKey.MoistureWarning);
        var criticalConfig = await context.SystemConfigs.SingleAsync(c => c.ConfigKey == LotQualityRecheckConstants.ConfigKey.MoistureCritical);
        warningConfig.ConfigValue = "15.0";
        criticalConfig.ConfigValue = "14.0"; // invalid
        context.SystemConfigs.UpdateRange(warningConfig, criticalConfig);

        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 1,
            LotCode = "LOT-001",
            LotType = "PADDY",
            WarehouseId = 1,
            StatusId = 2, // IN_STOCK
            InboundDate = DateTime.UtcNow.AddDays(-35),
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);
        await context.SaveChangesAsync();

        var service = new LotQualityRecheckService(
            context,
            new LotQualityRecheckRulesEngine(),
            _cacheMock.Object,
            _dispatcherMock.Object,
            _realtimeMock.Object,
            _loggerFactoryMock.Object);

        // Act
        var result = await service.EvaluateAllLotsAsync(CancellationToken.None);

        // Assert
        var configAlert = await context.Alerts.SingleAsync(a => a.AlertType == LotQualityRecheckConstants.AlertType.LotQualityRecheckConfigInvalid);
        configAlert.Message.Should().Contain("MoistureCriticalThreshold");

        // The lot should still evaluate other rules (e.g. InspectionDue)
        var lotAlerts = await context.Alerts.Where(a => a.RelatedEntityType == "PADDY_LOT").ToListAsync();
        lotAlerts.Should().ContainSingle(a => a.AlertType == LotQualityRecheckConstants.AlertType.InspectionDue);
    }
}
