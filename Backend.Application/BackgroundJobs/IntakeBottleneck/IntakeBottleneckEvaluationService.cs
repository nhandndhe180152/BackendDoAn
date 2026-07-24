using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Helpers;
using Backend.Share.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.BackgroundJobs.IntakeBottleneck;

public class IntakeBottleneckEvaluationService : IIntakeBottleneckEvaluationService
{
    private readonly IApplicationDbContext _context;
    private readonly IIntakeBottleneckQueryService _queryService;
    private readonly IIntakeBottleneckCalculator _calculator;
    private readonly ICacheService _cacheService;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IDataChangeNotifier _dataChangeNotifier;
    private readonly ILogger<IntakeBottleneckEvaluationService> _logger;

    public IntakeBottleneckEvaluationService(
        IApplicationDbContext context,
        IIntakeBottleneckQueryService queryService,
        IIntakeBottleneckCalculator calculator,
        ICacheService cacheService,
        INotificationDispatcher notificationDispatcher,
        IDataChangeNotifier dataChangeNotifier,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _queryService = queryService;
        _calculator = calculator;
        _cacheService = cacheService;
        _notificationDispatcher = notificationDispatcher;
        _dataChangeNotifier = dataChangeNotifier;
        _logger = loggerFactory.CreateLogger<IntakeBottleneckEvaluationService>();
    }

    public async Task<IntakeBottleneckJobResult> EvaluateAllAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new IntakeBottleneckJobResult();
        var lockKey = IntakeBottleneckConstants.Job.LockKeyPrefix;

        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("[IntakeBottleneck] Skip execution. Reason: lock_exists (Job is already running).");
            result.SkippedByLock = true;
            return result;
        }

        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(10));

        try
        {
            var warehouseIds = await _queryService.GetActiveWarehouseIdsAsync(cancellationToken);
            _logger.LogInformation("[IntakeBottleneck] Evaluation started. Active warehouses: {Count}", warehouseIds.Count);

            foreach (var whId in warehouseIds)
            {
                var whResult = await EvaluateWarehouseAsync(whId, cancellationToken);
                result.Processed++;

                if (whResult.Success)
                {
                    switch (whResult.Action)
                    {
                        case IntakeBottleneckConstants.Action.Created:
                            result.AlertsCreated++;
                            result.NotificationsQueued++;
                            break;
                        case IntakeBottleneckConstants.Action.Updated:
                        case IntakeBottleneckConstants.Action.Escalated:
                            result.AlertsUpdated++;
                            if (whResult.Action == IntakeBottleneckConstants.Action.Escalated)
                            {
                                result.NotificationsQueued++;
                            }
                            break;
                        case IntakeBottleneckConstants.Action.Resolved:
                            result.AlertsResolved++;
                            break;
                        case IntakeBottleneckConstants.Action.ConfigWarning:
                            // Config warning creates/updates config alerts
                            result.AlertsCreated++;
                            break;
                        case IntakeBottleneckConstants.Action.Skipped:
                            result.Skipped++;
                            break;
                    }
                }
                else
                {
                    result.Failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IntakeBottleneck] Critical error in recurrring evaluation job.");
            result.Failed++;
        }
        finally
        {
            await _cacheService.RemoveAsync(lockKey);
        }

        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    public async Task<IntakeBottleneckWarehouseResult> EvaluateWarehouseAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var result = new IntakeBottleneckWarehouseResult { WarehouseId = warehouseId };

        var whLockKey = $"{IntakeBottleneckConstants.Job.LockKeyPrefix}:warehouse:{warehouseId}";
        if (await _cacheService.ExistsAsync(whLockKey))
        {
            _logger.LogDebug("[IntakeBottleneck] Skip warehouse {Id} evaluation. Reason: lock exists.", warehouseId);
            result.Success = true;
            result.Action = IntakeBottleneckConstants.Action.Skipped;
            return result;
        }
        await _cacheService.SetAsync(whLockKey, "running", TimeSpan.FromMinutes(2));

        try
        {
            // 1. Load Configurations
            var (windowHours, warningRatio, criticalRatio) = await _queryService.GetGlobalThresholdConfigsAsync(cancellationToken);
            var labourCapacity = await _queryService.GetLabourCapacityAsync(warehouseId, cancellationToken);
            var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
            var warehouseName = warehouse?.Name ?? $"Kho {warehouseId}";

            var configErrors = new List<string>();
            if (windowHours == null || windowHours.Value <= 0) configErrors.Add(IntakeBottleneckConstants.ConfigKey.WindowHours);
            if (warningRatio == null || warningRatio.Value <= 0) configErrors.Add(IntakeBottleneckConstants.ConfigKey.WarningRatio);
            if (criticalRatio == null || criticalRatio.Value <= 0 || criticalRatio.Value <= warningRatio.Value) configErrors.Add(IntakeBottleneckConstants.ConfigKey.CriticalRatio);
            if (labourCapacity == null || labourCapacity.Value <= 0) configErrors.Add($"{IntakeBottleneckConstants.ConfigKey.LabourCapacityPrefix}{warehouseId}");

            using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                // Load existing alerts
                var activeBusinessAlert = await _context.Alerts
                    .FirstOrDefaultAsync(a => a.DeduplicationKey == $"{IntakeBottleneckConstants.Job.DeduplicationKeyPrefix}:{warehouseId}" &&
                                              !a.IsDeleted &&
                                              a.Status != AlertConstants.Status.Resolved, cancellationToken);

                var activeConfigAlert = await _context.Alerts
                    .FirstOrDefaultAsync(a => a.DeduplicationKey == $"{IntakeBottleneckConstants.Job.DeduplicationKeyPrefix}_CONFIG:{warehouseId}" &&
                                              !a.IsDeleted &&
                                              a.Status != AlertConstants.Status.Resolved, cancellationToken);

                bool shouldSendNotification = false;
                string? notificationMessage = null;
                string? notificationCode = null;
                Alert? alertToNotify = null;

                if (configErrors.Count > 0)
                {
                    result.Action = IntakeBottleneckConstants.Action.ConfigWarning;
                    var configMessage = $"Không thể đánh giá kho {warehouseName} vì thiếu hoặc sai cấu hình {string.Join(", ", configErrors)}. Vui lòng cập nhật cấu hình trước lần đánh giá tiếp theo.";

                    if (activeConfigAlert == null)
                    {
                        var newConfigAlert = new Alert
                        {
                            AlertType = IntakeBottleneckConstants.Job.ConfigAlertType,
                            Severity = AlertConstants.Severity.Warning,
                            WarehouseId = warehouseId,
                            Message = configMessage,
                            RelatedEntityType = AlertConstants.RelatedEntityType.Warehouse,
                            RelatedEntityId = warehouseId,
                            Status = AlertConstants.Status.Open,
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false,
                            DeduplicationKey = $"{IntakeBottleneckConstants.Job.DeduplicationKeyPrefix}_CONFIG:{warehouseId}"
                        };
                        await _context.Alerts.AddAsync(newConfigAlert, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);

                        alertToNotify = newConfigAlert;
                        shouldSendNotification = true;
                        notificationMessage = configMessage;
                        notificationCode = NotificationConstants.Code.IntakeBottleneckConfigAlert;
                    }
                    else
                    {
                        if (activeConfigAlert.Message != configMessage)
                        {
                            activeConfigAlert.Message = configMessage;
                            activeConfigAlert.LastModifiedDate = DateTime.UtcNow;
                            _context.Alerts.Update(activeConfigAlert);
                            await _context.SaveChangesAsync(cancellationToken);

                            alertToNotify = activeConfigAlert;
                            shouldSendNotification = true;
                            notificationMessage = configMessage;
                            notificationCode = NotificationConstants.Code.IntakeBottleneckConfigAlert;
                        }
                    }

                    // Nếu có business alert cũ, resolve nó
                    if (activeBusinessAlert != null)
                    {
                        activeBusinessAlert.Status = AlertConstants.Status.Resolved;
                        activeBusinessAlert.ResolvedAt = DateTime.UtcNow;
                        activeBusinessAlert.LastModifiedDate = DateTime.UtcNow;
                        activeBusinessAlert.DeduplicationKey = null;
                        _context.Alerts.Update(activeBusinessAlert);
                        await _context.SaveChangesAsync(cancellationToken);

                        await NotifySignalRAsync(activeBusinessAlert);
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }

                    if (shouldSendNotification && alertToNotify != null)
                    {
                        await NotifySignalRAsync(alertToNotify);
                        await SendNotificationAsync(notificationCode!, notificationMessage!, alertToNotify.Id);
                    }

                    result.Success = true;
                    return result;
                }

                // Cấu hình hợp lệ -> Resolve config alert nếu có
                if (activeConfigAlert != null)
                {
                    activeConfigAlert.Status = AlertConstants.Status.Resolved;
                    activeConfigAlert.ResolvedAt = DateTime.UtcNow;
                    activeConfigAlert.LastModifiedDate = DateTime.UtcNow;
                    activeConfigAlert.DeduplicationKey = null;
                    _context.Alerts.Update(activeConfigAlert);
                    await _context.SaveChangesAsync(cancellationToken);

                    await NotifySignalRAsync(activeConfigAlert);
                }

                // 2. Business Evaluation
                var windowStart = DateTimeHelper.VietnamNow();
                var windowEnd = windowStart.AddHours((double)windowHours!.Value);

                var expectedIntake = await _queryService.GetExpectedIntakeAsync(warehouseId, windowStart, windowEnd, cancellationToken);
                var freeStorage = await _queryService.GetFreeStorageCapacityAsync(warehouseId, cancellationToken);

                var calcInput = new IntakeBottleneckCalculationInput
                {
                    ExpectedIntakeKg = expectedIntake,
                    FreeStorageCapacityKg = freeStorage,
                    IntakeLabourCapacityKg = labourCapacity!.Value,
                    WarningRatio = warningRatio!.Value,
                    CriticalRatio = criticalRatio!.Value,
                    WindowStart = windowStart,
                    WindowEnd = windowEnd
                };

                var calcResult = _calculator.Calculate(calcInput);
                result.CalculationResult = calcResult;

                if (calcResult.Classification == IntakeBottleneckConstants.Classification.Normal)
                {
                    if (activeBusinessAlert != null)
                    {
                        activeBusinessAlert.Status = AlertConstants.Status.Resolved;
                        activeBusinessAlert.ResolvedAt = DateTime.UtcNow;
                        activeBusinessAlert.LastModifiedDate = DateTime.UtcNow;
                        activeBusinessAlert.DeduplicationKey = null;
                        _context.Alerts.Update(activeBusinessAlert);
                        await _context.SaveChangesAsync(cancellationToken);

                        result.Action = IntakeBottleneckConstants.Action.Resolved;
                        alertToNotify = activeBusinessAlert;
                    }
                    else
                    {
                        result.Action = IntakeBottleneckConstants.Action.Unchanged;
                    }
                }
                else
                {
                    var severity = calcResult.Classification == IntakeBottleneckConstants.Classification.Critical
                        ? AlertConstants.Severity.Critical
                        : AlertConstants.Severity.Warning;

                    string message;
                    var startStr = windowStart.ToString("dd/MM/yyyy HH:mm");
                    var endStr = windowEnd.ToString("dd/MM/yyyy HH:mm");

                    if (severity == AlertConstants.Severity.Critical)
                    {
                        message = $"Khối lượng lúa dự kiến của kho {warehouseName} đã vượt hoặc chạm ngưỡng nghiêm trọng. Dự kiến {calcResult.ExpectedIntakeKg:0.##} kg; năng lực tiếp nhận hiệu dụng {calcResult.EffectiveCapacityKg:0.##} kg. Vui lòng điều chỉnh lịch, nhân công hoặc sức chứa kho.";
                    }
                    else
                    {
                        message = $"Kho {warehouseName} dự kiến tiếp nhận {calcResult.ExpectedIntakeKg:0.##} kg lúa từ {startStr} đến {endStr}. Sức chứa cột còn {calcResult.FreeStorageCapacityKg:0.##} kg, năng lực nhân công {calcResult.IntakeLabourCapacityKg:0.##} kg. Nguồn giới hạn: {calcResult.LimitingResource}. Mức sử dụng: {(calcResult.BottleneckRatio * 100):0.##}%.";
                    }

                    if (activeBusinessAlert == null)
                    {
                        var newAlert = new Alert
                        {
                            AlertType = AlertConstants.Type.IntakeBottleneck,
                            Severity = severity,
                            WarehouseId = warehouseId,
                            Message = message,
                            RelatedEntityType = AlertConstants.RelatedEntityType.Warehouse,
                            RelatedEntityId = warehouseId,
                            Status = AlertConstants.Status.Open,
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false,
                            DeduplicationKey = $"{IntakeBottleneckConstants.Job.DeduplicationKeyPrefix}:{warehouseId}"
                        };
                        await _context.Alerts.AddAsync(newAlert, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);

                        result.Action = IntakeBottleneckConstants.Action.Created;
                        alertToNotify = newAlert;
                        shouldSendNotification = true;
                        notificationMessage = message;
                        notificationCode = NotificationConstants.Code.IntakeBottleneckAlert;
                    }
                    else
                    {
                        bool hasChange = false;
                        var oldSeverity = activeBusinessAlert.Severity;

                        if (activeBusinessAlert.Severity != severity)
                        {
                            activeBusinessAlert.Severity = severity;
                            hasChange = true;
                        }

                        if (activeBusinessAlert.Message != message)
                        {
                            activeBusinessAlert.Message = message;
                            hasChange = true;
                        }

                        if (hasChange)
                        {
                            activeBusinessAlert.LastModifiedDate = DateTime.UtcNow;

                            // Escalation: Reopen OPEN nếu alert đã ACK và severity tăng lên
                            if (GetSeverityPriority(severity) > GetSeverityPriority(oldSeverity))
                            {
                                activeBusinessAlert.Status = AlertConstants.Status.Open;
                                result.Action = IntakeBottleneckConstants.Action.Escalated;
                                shouldSendNotification = true;
                            }
                            else
                            {
                                result.Action = IntakeBottleneckConstants.Action.Downgraded;
                            }

                            _context.Alerts.Update(activeBusinessAlert);
                            await _context.SaveChangesAsync(cancellationToken);

                            alertToNotify = activeBusinessAlert;
                            notificationMessage = message;
                            notificationCode = NotificationConstants.Code.IntakeBottleneckAlert;
                        }
                        else
                        {
                            result.Action = IntakeBottleneckConstants.Action.Unchanged;
                        }
                    }
                }

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                // 3. Realtime notifications sau commit thành công
                if (alertToNotify != null)
                {
                    await NotifySignalRAsync(alertToNotify);
                }

                if (shouldSendNotification && notificationMessage != null && notificationCode != null)
                {
                    await SendNotificationAsync(notificationCode, notificationMessage, alertToNotify.Id);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                if (_context is DbContext dbCtx)
                {
                    dbCtx.ChangeTracker.Clear();
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IntakeBottleneck] Lỗi khi đánh giá kho {WarehouseId}.", warehouseId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Action = IntakeBottleneckConstants.Action.Failed;
        }
        finally
        {
            await _cacheService.RemoveAsync(whLockKey);
        }

        return result;
    }

    private int GetSeverityPriority(string sev)
    {
        return sev switch
        {
            AlertConstants.Severity.Critical => 3,
            AlertConstants.Severity.Warning => 2,
            _ => 1
        };
    }

    private async Task NotifySignalRAsync(Alert alert)
    {
        try
        {
            var payload = new
            {
                alertId = alert.Id,
                alertType = alert.AlertType,
                status = alert.Status,
                severity = alert.Severity,
                warehouseId = alert.WarehouseId,
                updatedAt = alert.LastModifiedDate ?? alert.CreatedDate
            };
            await _dataChangeNotifier.NotifyAlertChangedAsync(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IntakeBottleneck] Lỗi phát realtime SignalR cho alert {Id}.", alert.Id);
        }
    }

    private async Task SendNotificationAsync(string code, string message, int alertId)
    {
        try
        {
            var target = new NotificationTarget
            {
                RoleIds = new List<int>
                {
                    CommonConstants.Role.ADMIN,
                    CommonConstants.Role.OWNER
                }
            };

            await _notificationDispatcher.DispatchAsync(
                code,
                target,
                new object[] { message },
                directionId: "/admin/alerts",
                createdBy: CommonConstants.ADMIN_USER,
                referenceType: NotificationConstants.ReferenceType.Alert,
                referenceId: alertId > 0 ? alertId : null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IntakeBottleneck] Lỗi gửi notification qua dispatcher cho code {Code}.", code);
        }
    }
}
