using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.IntakeBottleneck;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Share.Helpers;
using Backend.Share.Services;
using Backend.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.IntakeBottleneck;

public class IntakeBottleneckEvaluationTests
{
    private readonly Mock<ISystemLookup> _lookupMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<INotificationDispatcher> _dispatcherMock = new();
    private readonly Mock<IDataChangeNotifier> _notifierMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<IntakeBottleneckQueryService>> _queryLoggerMock = new();
    private readonly Mock<ILogger<IntakeBottleneckEvaluationService>> _evalLoggerMock = new();

    public IntakeBottleneckEvaluationTests()
    {
        _lookupMock.Setup(l => l.PaddyScheduleStatusId("CONFIRMED")).Returns(2);
        _lookupMock.Setup(l => l.PaddyScheduleStatusId("COLLECTING")).Returns(3);
        _lookupMock.Setup(l => l.PaddyScheduleStatusId("STOCKED")).Returns(5);

        _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("IntakeBottleneckQueryService"))))
            .Returns(_queryLoggerMock.Object);
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("IntakeBottleneckEvaluationService"))))
            .Returns(_evalLoggerMock.Object);
    }

    private BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BackendContext(options);
    }

    // ── 1. CALCULATOR TESTS ──────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1000, 1000, 0.8, 1.2, IntakeBottleneckConstants.Classification.Normal, IntakeBottleneckConstants.LimitingResource.None)]
    [InlineData(500, 1000, 1000, 0.8, 1.2, IntakeBottleneckConstants.Classification.Normal, IntakeBottleneckConstants.LimitingResource.Both)] // ratios both 0.5 < 0.8
    [InlineData(900, 1000, 1000, 0.8, 1.2, IntakeBottleneckConstants.Classification.Warning, IntakeBottleneckConstants.LimitingResource.Both)] // ratio 0.9 >= 0.8
    [InlineData(1300, 1000, 1000, 0.8, 1.2, IntakeBottleneckConstants.Classification.Critical, IntakeBottleneckConstants.LimitingResource.Both)] // ratio 1.3 >= 1.2
    [InlineData(900, 1000, 2000, 0.8, 1.2, IntakeBottleneckConstants.Classification.Warning, IntakeBottleneckConstants.LimitingResource.Storage)] // storage ratio 0.9, labour ratio 0.45
    [InlineData(900, 2000, 1000, 0.8, 1.2, IntakeBottleneckConstants.Classification.Warning, IntakeBottleneckConstants.LimitingResource.Labour)] // storage ratio 0.45, labour ratio 0.9
    public void Calculator_ShouldReturnCorrectClassificationAndLimitingResource(
        decimal expectedIntake,
        decimal freeStorage,
        decimal labourCapacity,
        decimal warningRatio,
        decimal criticalRatio,
        string expectedClass,
        string expectedResource)
    {
        // Arrange
        var calculator = new IntakeBottleneckCalculator();
        var input = new IntakeBottleneckCalculationInput
        {
            ExpectedIntakeKg = expectedIntake,
            FreeStorageCapacityKg = freeStorage,
            IntakeLabourCapacityKg = labourCapacity,
            WarningRatio = warningRatio,
            CriticalRatio = criticalRatio,
            WindowStart = DateTime.UtcNow,
            WindowEnd = DateTime.UtcNow.AddHours(24)
        };

        // Act
        var result = calculator.Calculate(input);

        // Assert
        result.HasConfigError.Should().BeFalse();
        result.Classification.Should().Be(expectedClass);
        result.LimitingResource.Should().Be(expectedResource);
    }

    [Fact]
    public void Calculator_WhenFreeStorageCapacityIsZero_AndExpectedIntakeGreaterThanZero_ShouldReturnCriticalAlertOnStorage()
    {
        // Arrange
        var calculator = new IntakeBottleneckCalculator();
        var input = new IntakeBottleneckCalculationInput
        {
            ExpectedIntakeKg = 500,
            FreeStorageCapacityKg = 0,
            IntakeLabourCapacityKg = 1000,
            WarningRatio = 0.8m,
            CriticalRatio = 1.2m,
            WindowStart = DateTime.UtcNow,
            WindowEnd = DateTime.UtcNow.AddHours(24)
        };

        // Act
        var result = calculator.Calculate(input);

        // Assert
        result.Classification.Should().Be(IntakeBottleneckConstants.Classification.Critical);
        result.LimitingResource.Should().Be(IntakeBottleneckConstants.LimitingResource.Storage);
        result.StorageLoadRatio.Should().Be(decimal.MaxValue);
    }

    [Fact]
    public void Calculator_WhenConfigurationRatiosAreInvalid_ShouldReturnConfigError()
    {
        // Arrange
        var calculator = new IntakeBottleneckCalculator();
        var input = new IntakeBottleneckCalculationInput
        {
            ExpectedIntakeKg = 500,
            FreeStorageCapacityKg = 1000,
            IntakeLabourCapacityKg = 1000,
            WarningRatio = 0.8m,
            CriticalRatio = 0.5m, // Invalid: critical <= warning
            WindowStart = DateTime.UtcNow,
            WindowEnd = DateTime.UtcNow.AddHours(24)
        };

        // Act
        var result = calculator.Calculate(input);

        // Assert
        result.HasConfigError.Should().BeTrue();
        result.ConfigErrorDetail.Should().Contain("Critical threshold");
    }

    // ── 2. QUERY SERVICE TESTS ───────────────────────────────────────────────

    [Fact]
    public async Task QueryService_GetExpectedIntake_ShouldOnlyIncludeConfirmedOrCollectingSchedulesInWindow()
    {
        // Arrange
        using var context = CreateContext();
        var whId = 1;
        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(1);

        // Valid schedule 1 (Confirmed)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 1, WarehouseId = whId, StatusId = 2, ScheduleDate = start.AddHours(4), EstimatedQtyKg = 100, IsDeleted = false, ScheduleCode = "S1"
        });
        // Valid schedule 2 (Collecting)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 2, WarehouseId = whId, StatusId = 3, ScheduleDate = start.AddHours(12), EstimatedQtyKg = 200, IsDeleted = false, ScheduleCode = "S2"
        });
        // Invalid status (Stocked - 5)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 3, WarehouseId = whId, StatusId = 5, ScheduleDate = start.AddHours(6), EstimatedQtyKg = 500, IsDeleted = false, ScheduleCode = "S3"
        });
        // Out of window (Too late)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 4, WarehouseId = whId, StatusId = 2, ScheduleDate = end.AddHours(1), EstimatedQtyKg = 1000, IsDeleted = false, ScheduleCode = "S4"
        });
        // Deleted
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 5, WarehouseId = whId, StatusId = 2, ScheduleDate = start.AddHours(8), EstimatedQtyKg = 500, IsDeleted = true, ScheduleCode = "S5"
        });
        await context.SaveChangesAsync();

        var queryService = new IntakeBottleneckQueryService(context, _loggerFactoryMock.Object, _lookupMock.Object);

        // Act
        var total = await queryService.GetExpectedIntakeAsync(whId, start, end, CancellationToken.None);

        // Assert
        total.Should().Be(300); // 100 + 200
    }

    [Fact]
    public async Task QueryService_GetFreeStorageCapacity_ShouldCorrectlyExcludeIncompatibleLocations()
    {
        // Arrange
        using var context = CreateContext();
        var whId = 1;

        // Compatible location (AllowedCategoryId = null, MaxCapacity = 1000, Current = 200) -> free 800
        context.Locations.Add(new Backend.Domain.Entities.Location
        {
            Id = 1, WarehouseId = whId, IsActive = true, IsDeleted = false, IsQuarantine = false, MaxCapacity = 1000, CurrentOccupancy = 200, ZoneName = "ZoneA"
        });

        // Incompatible category location (AllowedCategoryId = 102 - Rice) -> Skip
        context.Locations.Add(new Backend.Domain.Entities.Location
        {
            Id = 2, WarehouseId = whId, IsActive = true, IsDeleted = false, IsQuarantine = false, MaxCapacity = 1000, CurrentOccupancy = 0, AllowedCategoryId = 102, ZoneName = "ZoneA"
        });

        // Quarantine location -> Skip
        context.Locations.Add(new Backend.Domain.Entities.Location
        {
            Id = 3, WarehouseId = whId, IsActive = true, IsDeleted = false, IsQuarantine = true, MaxCapacity = 1000, CurrentOccupancy = 0, ZoneName = "ZoneA"
        });

        // SingleTypeColumn containing Rice (ProductCategoryId = 102) -> Skip
        var riceVariant = new Backend.Domain.Entities.ProductVariant 
        { 
            Id = 1, 
            Product = new Backend.Domain.Entities.Product { Id = 1, ProductCategoryId = 102, Name = "Rice" }, 
            SKU = "V1", 
            Name = "Variant1" 
        };
        context.ProductVariants.Add(riceVariant);
        context.Locations.Add(new Backend.Domain.Entities.Location
        {
            Id = 4, WarehouseId = whId, IsActive = true, IsDeleted = false, IsQuarantine = false, MaxCapacity = 1000, CurrentOccupancy = 300, IsSingleTypeColumn = true, CurrentProductVariantId = 1, ZoneName = "ZoneA"
        });

        await context.SaveChangesAsync();

        var queryService = new IntakeBottleneckQueryService(context, _loggerFactoryMock.Object, _lookupMock.Object);

        // Act
        var freeStorage = await queryService.GetFreeStorageCapacityAsync(whId, CancellationToken.None);

        // Assert
        freeStorage.Should().Be(800);
    }

    // ── 3. EVALUATION SERVICE WORKFLOW TESTS ──────────────────────────────────

    [Fact]
    public async Task EvaluationService_WhenNormalAndNoAlert_ShouldDoNothing()
    {
        // Arrange
        using var context = CreateContext();
        var whId = 1;

        // Seed global configs
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWindowHours", ConfigValue = "24", IsDeleted = false, Name = "WindowHours" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWarningRatio", ConfigValue = "0.8", IsDeleted = false, Name = "WarningRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckCriticalRatio", ConfigValue = "1.2", IsDeleted = false, Name = "CriticalRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = $"IntakeLabourCapacity:{whId}", ConfigValue = "1000", IsDeleted = false, Name = "LabourCapacity" });

        // Seed warehouse & location (Max = 1000, Occupancy = 0) -> free storage = 1000
        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = whId, IsActive = true, IsDeleted = false, Code = "W1", Name = "Kho A" });
        context.Locations.Add(new Backend.Domain.Entities.Location { Id = 1, WarehouseId = whId, IsActive = true, IsDeleted = false, MaxCapacity = 1000, CurrentOccupancy = 0, ZoneName = "Z" });
        
        // Expected intake is 500 (ratio = 0.5 < 0.8 Warning)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 1, WarehouseId = whId, StatusId = 2, ScheduleDate = DateTimeHelper.VietnamNow().AddHours(2), EstimatedQtyKg = 500, IsDeleted = false, ScheduleCode = "S1"
        });
        await context.SaveChangesAsync();

        var queryService = new IntakeBottleneckQueryService(context, _loggerFactoryMock.Object, _lookupMock.Object);
        var calculator = new IntakeBottleneckCalculator();
        var evalService = new IntakeBottleneckEvaluationService(
            context, queryService, calculator, _cacheMock.Object, _dispatcherMock.Object, _notifierMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await evalService.EvaluateWarehouseAsync(whId, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Action.Should().Be("UNCHANGED");
        context.Alerts.Count(a => !a.IsDeleted && a.Status != AlertConstants.Status.Resolved).Should().Be(0);
    }

    [Fact]
    public async Task EvaluationService_WhenWarning_ShouldCreateAlertAndNotify()
    {
        // Arrange
        using var context = CreateContext();
        var whId = 1;

        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWindowHours", ConfigValue = "24", IsDeleted = false, Name = "WindowHours" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWarningRatio", ConfigValue = "0.8", IsDeleted = false, Name = "WarningRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckCriticalRatio", ConfigValue = "1.2", IsDeleted = false, Name = "CriticalRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = $"IntakeLabourCapacity:{whId}", ConfigValue = "1000", IsDeleted = false, Name = "LabourCapacity" });

        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = whId, IsActive = true, IsDeleted = false, Code = "W1", Name = "Kho A" });
        context.Locations.Add(new Backend.Domain.Entities.Location { Id = 1, WarehouseId = whId, IsActive = true, IsDeleted = false, MaxCapacity = 1000, CurrentOccupancy = 0, ZoneName = "Z" });

        // Expected intake is 900 (ratio = 0.9 >= 0.8 Warning)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 1, WarehouseId = whId, StatusId = 2, ScheduleDate = DateTimeHelper.VietnamNow().AddHours(2), EstimatedQtyKg = 900, IsDeleted = false, ScheduleCode = "S1"
        });
        await context.SaveChangesAsync();

        var queryService = new IntakeBottleneckQueryService(context, _loggerFactoryMock.Object, _lookupMock.Object);
        var calculator = new IntakeBottleneckCalculator();
        var evalService = new IntakeBottleneckEvaluationService(
            context, queryService, calculator, _cacheMock.Object, _dispatcherMock.Object, _notifierMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await evalService.EvaluateWarehouseAsync(whId, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Action.Should().Be("CREATED");

        var alerts = context.Alerts.Where(a => a.WarehouseId == whId && !a.IsDeleted && a.Status == AlertConstants.Status.Open).ToList();
        alerts.Should().HaveCount(1);
        alerts[0].Severity.Should().Be(AlertConstants.Severity.Warning);
        alerts[0].AlertType.Should().Be(AlertConstants.Type.IntakeBottleneck);

        _notifierMock.Verify(n => n.NotifyAlertChangedAsync(It.IsAny<object>()), Times.Once);
        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.IntakeBottleneckAlert,
            It.IsAny<NotificationTarget>(),
            It.IsAny<object[]>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task EvaluationService_WhenEscalated_ShouldUpdateAlertAndReopen()
    {
        // Arrange
        using var context = CreateContext();
        var whId = 1;

        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWindowHours", ConfigValue = "24", IsDeleted = false, Name = "WindowHours" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWarningRatio", ConfigValue = "0.8", IsDeleted = false, Name = "WarningRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckCriticalRatio", ConfigValue = "1.2", IsDeleted = false, Name = "CriticalRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = $"IntakeLabourCapacity:{whId}", ConfigValue = "1000", IsDeleted = false, Name = "LabourCapacity" });

        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = whId, IsActive = true, IsDeleted = false, Code = "W1", Name = "Kho A" });
        context.Locations.Add(new Backend.Domain.Entities.Location { Id = 1, WarehouseId = whId, IsActive = true, IsDeleted = false, MaxCapacity = 1000, CurrentOccupancy = 0, ZoneName = "Z" });

        // Add pre-existing Acknowledged Warning Alert
        var existingAlert = new Backend.Domain.Entities.Alert
        {
            Id = 100,
            AlertType = AlertConstants.Type.IntakeBottleneck,
            Severity = AlertConstants.Severity.Warning,
            WarehouseId = whId,
            Status = AlertConstants.Status.Acknowledged,
            DeduplicationKey = $"INTAKE_BOTTLENECK:{whId}",
            Message = "Old message",
            CreatedDate = DateTime.UtcNow.AddHours(-1)
        };
        context.Alerts.Add(existingAlert);

        // Expected intake is 1300 (ratio = 1.3 >= 1.2 Critical)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 1, WarehouseId = whId, StatusId = 2, ScheduleDate = DateTimeHelper.VietnamNow().AddHours(2), EstimatedQtyKg = 1300, IsDeleted = false, ScheduleCode = "S1"
        });
        await context.SaveChangesAsync();

        var queryService = new IntakeBottleneckQueryService(context, _loggerFactoryMock.Object, _lookupMock.Object);
        var calculator = new IntakeBottleneckCalculator();
        var evalService = new IntakeBottleneckEvaluationService(
            context, queryService, calculator, _cacheMock.Object, _dispatcherMock.Object, _notifierMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await evalService.EvaluateWarehouseAsync(whId, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Action.Should().Be("ESCALATED");

        var updatedAlert = context.Alerts.Find(100);
        updatedAlert!.Severity.Should().Be(AlertConstants.Severity.Critical);
        updatedAlert.Status.Should().Be(AlertConstants.Status.Open); // Reopened

        _notifierMock.Verify(n => n.NotifyAlertChangedAsync(It.IsAny<object>()), Times.Once);
        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.IntakeBottleneckAlert,
            It.IsAny<NotificationTarget>(),
            It.IsAny<object[]>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task EvaluationService_WhenNormalAfterAlert_ShouldResolveAlert()
    {
        // Arrange
        using var context = CreateContext();
        var whId = 1;

        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWindowHours", ConfigValue = "24", IsDeleted = false, Name = "WindowHours" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckWarningRatio", ConfigValue = "0.8", IsDeleted = false, Name = "WarningRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = "IntakeBottleneckCriticalRatio", ConfigValue = "1.2", IsDeleted = false, Name = "CriticalRatio" });
        context.SystemConfigs.Add(new Backend.Domain.Entities.SystemConfig { ConfigKey = $"IntakeLabourCapacity:{whId}", ConfigValue = "1000", IsDeleted = false, Name = "LabourCapacity" });

        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = whId, IsActive = true, IsDeleted = false, Code = "W1", Name = "Kho A" });
        context.Locations.Add(new Backend.Domain.Entities.Location { Id = 1, WarehouseId = whId, IsActive = true, IsDeleted = false, MaxCapacity = 1000, CurrentOccupancy = 0, ZoneName = "Z" });

        // Add pre-existing Open Alert
        var existingAlert = new Backend.Domain.Entities.Alert
        {
            Id = 100,
            AlertType = AlertConstants.Type.IntakeBottleneck,
            Severity = AlertConstants.Severity.Warning,
            WarehouseId = whId,
            Status = AlertConstants.Status.Open,
            DeduplicationKey = $"INTAKE_BOTTLENECK:{whId}",
            Message = "Old warning message",
            CreatedDate = DateTime.UtcNow.AddHours(-1)
        };
        context.Alerts.Add(existingAlert);

        // Expected intake is 200 (ratio = 0.2 < 0.8 Normal)
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 1, WarehouseId = whId, StatusId = 2, ScheduleDate = DateTimeHelper.VietnamNow().AddHours(2), EstimatedQtyKg = 200, IsDeleted = false, ScheduleCode = "S1"
        });
        await context.SaveChangesAsync();

        var queryService = new IntakeBottleneckQueryService(context, _loggerFactoryMock.Object, _lookupMock.Object);
        var calculator = new IntakeBottleneckCalculator();
        var evalService = new IntakeBottleneckEvaluationService(
            context, queryService, calculator, _cacheMock.Object, _dispatcherMock.Object, _notifierMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await evalService.EvaluateWarehouseAsync(whId, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Action.Should().Be("RESOLVED");

        var resolvedAlert = context.Alerts.Find(100);
        resolvedAlert!.Status.Should().Be(AlertConstants.Status.Resolved);
        resolvedAlert.ResolvedAt.Should().NotBeNull();
        resolvedAlert.DeduplicationKey.Should().BeNull(); // Deduplication cleared

        _notifierMock.Verify(n => n.NotifyAlertChangedAsync(It.IsAny<object>()), Times.Once);
        // Resolve should not trigger email/FCM notification
        _dispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<string>(),
            It.IsAny<NotificationTarget>(),
            It.IsAny<object[]>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task EvaluationService_WhenConfigIsMissing_ShouldCreateConfigWarningAlert()
    {
        // Arrange
        using var context = CreateContext();
        var whId = 1;

        // No configurations are seeded
        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = whId, IsActive = true, IsDeleted = false, Code = "W1", Name = "Kho A" });
        await context.SaveChangesAsync();

        var queryService = new IntakeBottleneckQueryService(context, _loggerFactoryMock.Object, _lookupMock.Object);
        var calculator = new IntakeBottleneckCalculator();
        var evalService = new IntakeBottleneckEvaluationService(
            context, queryService, calculator, _cacheMock.Object, _dispatcherMock.Object, _notifierMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await evalService.EvaluateWarehouseAsync(whId, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Action.Should().Be("CONFIG_WARNING");

        var configAlerts = context.Alerts.Where(a => a.WarehouseId == whId && a.AlertType == "INTAKE_BOTTLENECK_CONFIGURATION" && !a.IsDeleted).ToList();
        configAlerts.Should().HaveCount(1);
        configAlerts[0].Severity.Should().Be(AlertConstants.Severity.Warning);
        configAlerts[0].Message.Should().Contain("thiếu hoặc sai cấu hình");

        _notifierMock.Verify(n => n.NotifyAlertChangedAsync(It.IsAny<object>()), Times.Once);
        _dispatcherMock.Verify(d => d.DispatchAsync(
            NotificationConstants.Code.IntakeBottleneckConfigAlert,
            It.IsAny<NotificationTarget>(),
            It.IsAny<object[]>(),
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Once);
    }
}

