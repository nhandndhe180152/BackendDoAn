using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

namespace Backend.Application.BackgroundJobs.LotQualityRecheck;

public class LotQualityRecheckService : ILotQualityRecheckService
{
    private readonly IApplicationDbContext _context;
    private readonly ILotQualityRecheckRulesEngine _rulesEngine;
    private readonly ICacheService _cacheService;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IDataChangeNotifier _dataChangeNotifier;
    private readonly ILogger<LotQualityRecheckService> _logger;

    public LotQualityRecheckService(
        IApplicationDbContext context,
        ILotQualityRecheckRulesEngine rulesEngine,
        ICacheService cacheService,
        INotificationDispatcher notificationDispatcher,
        IDataChangeNotifier dataChangeNotifier,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _rulesEngine = rulesEngine;
        _cacheService = cacheService;
        _notificationDispatcher = notificationDispatcher;
        _dataChangeNotifier = dataChangeNotifier;
        _logger = loggerFactory.CreateLogger<LotQualityRecheckService>();
    }

    public async Task<LotQualityRecheckJobResult> EvaluateAllLotsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new LotQualityRecheckJobResult();
        var lockKey = LotQualityRecheckConstants.Job.LockKeyPrefix;

        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("{LogPrefix} Skip execution. Reason: lock_exists (Job is already running).", LotQualityRecheckConstants.Job.LogPrefix);
            result.SkippedByLock = true;
            return result;
        }

        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(10));

        try
        {
            int lastId = 0;
            const int batchSize = 1000;
            bool hasMore = true;
            var businessToday = DateTimeHelper.VietnamNow();

            _logger.LogInformation("{LogPrefix} Periodic evaluation started. BusinessToday: {Today}", LotQualityRecheckConstants.Job.LogPrefix, businessToday);

            // Pre-load all warehouses to check configuration overrides
            var activeWarehouseIds = await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.IsActive && !w.IsDeleted)
                .Select(w => w.Id)
                .ToListAsync(cancellationToken);

            var configsMap = new Dictionary<int, (LotQualityRecheckWarehouseConfig Config, List<string> Errors)>();
            foreach (var whId in activeWarehouseIds)
            {
                var (config, errors) = await GetWarehouseConfigAsync(whId, cancellationToken);
                configsMap[whId] = (config, errors);

                // Manage configuration alerts immediately
                await ManageConfigAlertAsync(whId, errors, cancellationToken);
            }

            while (hasMore)
            {
                // Query batch of active PaddyLots using cursor pagination
                var lots = await _context.PaddyLots
                    .AsNoTracking()
                    .Include(l => l.Status)
                    .Include(l => l.Location)
                    .Where(l => l.Id > lastId &&
                                !l.IsDeleted &&
                                l.Status.Code != LotStatusCodeConstants.Depleted &&
                                l.RemainingWeightKg > 0)
                    .OrderBy(l => l.Id)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (lots.Count == 0)
                {
                    hasMore = false;
                    break;
                }

                var lotIds = lots.Select(l => l.Id).ToList();

                // Batch load the latest inspections to avoid N+1 queries
                var inspections = await _context.QualityInspections
                    .AsNoTracking()
                    .Where(qi => lotIds.Contains(qi.PaddyLotId) && !qi.IsDeleted)
                    .OrderByDescending(qi => qi.InspectedAt)
                    .ThenByDescending(qi => qi.Id)
                    .ToListAsync(cancellationToken);

                var latestInspections = inspections
                    .GroupBy(qi => qi.PaddyLotId)
                    .ToDictionary(g => g.Key, g => g.First());

                // Batch load active alerts for these lots to avoid N+1 queries
                var activeAlerts = await _context.Alerts
                    .Where(a => a.RelatedEntityType == AlertConstants.RelatedEntityType.PaddyLot &&
                                a.RelatedEntityId.HasValue &&
                                lotIds.Contains(a.RelatedEntityId.Value) &&
                                a.Status != AlertConstants.Status.Resolved &&
                                !a.IsDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var lot in lots)
                {
                    lastId = lot.Id;
                    result.Processed++;

                    // Resolve config for the lot's warehouse
                    if (!configsMap.TryGetValue(lot.WarehouseId, out var whConfigTuple))
                    {
                        _logger.LogWarning("{LogPrefix} Skip lot {LotId}. Reason: Warehouse config missing.", LotQualityRecheckConstants.Job.LogPrefix, lot.Id);
                        result.Skipped++;
                        continue;
                    }

                    var config = whConfigTuple.Config;
                    var configErrors = whConfigTuple.Errors;

                    latestInspections.TryGetValue(lot.Id, out var latestInspection);
                    var activeAlertsForLot = activeAlerts.Where(a => a.RelatedEntityId == lot.Id).ToList();

                    try
                    {
                        var lotResult = await EvaluateLotCoreAsync(
                            lot,
                            config,
                            configErrors,
                            latestInspection,
                            activeAlertsForLot,
                            businessToday,
                            cancellationToken);

                        if (lotResult.Success)
                        {
                            foreach (var act in lotResult.Actions)
                            {
                                if (act == LotQualityRecheckConstants.Action.Created) result.AlertsCreated++;
                                else if (act == LotQualityRecheckConstants.Action.Updated || act == LotQualityRecheckConstants.Action.Escalated) result.AlertsUpdated++;
                                else if (act == LotQualityRecheckConstants.Action.Resolved) result.AlertsResolved++;

                                if (act == LotQualityRecheckConstants.Action.Created || act == LotQualityRecheckConstants.Action.Escalated)
                                {
                                    result.NotificationsQueued++;
                                }
                            }
                        }
                        else
                        {
                            result.Failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{LogPrefix} Failed to evaluate lot {LotId}.", LotQualityRecheckConstants.Job.LogPrefix, lot.Id);
                        result.Failed++;
                    }
                }

                if (lots.Count < batchSize)
                {
                    hasMore = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LogPrefix} Critical error in evaluation scan job.", LotQualityRecheckConstants.Job.LogPrefix);
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

    public async Task<LotQualityRecheckLotEvaluationResult> EvaluateLotAsync(int lotId, CancellationToken cancellationToken)
    {
        var result = new LotQualityRecheckLotEvaluationResult { LotId = lotId, Success = false };
        var lockKey = $"{LotQualityRecheckConstants.Job.LockKeyPrefix}:lot:{lotId}";

        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("{LogPrefix} Skip targeted evaluation for lot {LotId}. Reason: lock exists.", LotQualityRecheckConstants.Job.LogPrefix, lotId);
            return result;
        }

        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(2));

        try
        {
            var businessToday = DateTimeHelper.VietnamNow();

            var lot = await _context.PaddyLots
                .Include(l => l.Status)
                .Include(l => l.Location)
                .FirstOrDefaultAsync(l => l.Id == lotId && !l.IsDeleted, cancellationToken);

            if (lot == null || lot.Status.Code == LotStatusCodeConstants.Depleted || lot.RemainingWeightKg <= 0)
            {
                _logger.LogDebug("{LogPrefix} Lot {LotId} is not eligible for evaluation (deleted, depleted or empty).", LotQualityRecheckConstants.Job.LogPrefix, lotId);
                result.Success = true;
                return result;
            }

            var (config, configErrors) = await GetWarehouseConfigAsync(lot.WarehouseId, cancellationToken);

            // Manage config alerts
            await ManageConfigAlertAsync(lot.WarehouseId, configErrors, cancellationToken);

            var latestInspection = await _context.QualityInspections
                .AsNoTracking()
                .Where(qi => qi.PaddyLotId == lotId && !qi.IsDeleted)
                .OrderByDescending(qi => qi.InspectedAt)
                .ThenByDescending(qi => qi.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var activeAlertsForLot = await _context.Alerts
                .Where(a => a.RelatedEntityType == AlertConstants.RelatedEntityType.PaddyLot &&
                            a.RelatedEntityId == lotId &&
                            a.Status != AlertConstants.Status.Resolved &&
                            !a.IsDeleted)
                .ToListAsync(cancellationToken);

            result = await EvaluateLotCoreAsync(
                lot,
                config,
                configErrors,
                latestInspection,
                activeAlertsForLot,
                businessToday,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LogPrefix} Error during targeted evaluation for lot {LotId}.", LotQualityRecheckConstants.Job.LogPrefix, lotId);
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            await _cacheService.RemoveAsync(lockKey);
        }

        return result;
    }

    private async Task<LotQualityRecheckLotEvaluationResult> EvaluateLotCoreAsync(
        PaddyLot lot,
        LotQualityRecheckWarehouseConfig config,
        List<string> configErrors,
        QualityInspection? latestInspection,
        List<Alert> activeAlertsForLot,
        DateTime businessToday,
        CancellationToken cancellationToken)
    {
        var result = new LotQualityRecheckLotEvaluationResult { LotId = lot.Id, Success = true };

        // 1. Evaluate rules
        var evaluations = _rulesEngine.Evaluate(lot, latestInspection, config, configErrors, businessToday);

        // 2. Start database transaction
        using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var notificationsToSend = new List<(Alert Alert, string Code, string Message)>();
        bool shouldNotifyRealtime = false;

        try
        {
            foreach (var eval in evaluations)
            {
                var existingAlert = activeAlertsForLot.FirstOrDefault(a => a.AlertType == eval.AlertType);

                if (eval.ShouldAlert)
                {
                    if (existingAlert == null)
                    {
                        var newAlert = new Alert
                        {
                            AlertType = eval.AlertType,
                            Severity = eval.Severity,
                            WarehouseId = lot.WarehouseId,
                            ProductVariantId = lot.ProductVariantId,
                            LocationId = lot.LocationId,
                            Message = eval.Message,
                            RelatedEntityType = AlertConstants.RelatedEntityType.PaddyLot,
                            RelatedEntityId = lot.Id,
                            Status = AlertConstants.Status.Open,
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false,
                            DeduplicationKey = eval.DeduplicationKey
                        };

                        await _context.Alerts.AddAsync(newAlert, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);

                        result.Actions.Add(LotQualityRecheckConstants.Action.Created);
                        notificationsToSend.Add((newAlert, GetNotificationCode(eval.AlertType), eval.Message));
                        shouldNotifyRealtime = true;
                    }
                    else
                    {
                        bool hasChange = false;
                        var oldSeverity = existingAlert.Severity;

                        if (existingAlert.Severity != eval.Severity)
                        {
                            existingAlert.Severity = eval.Severity;
                            hasChange = true;
                        }

                        if (existingAlert.Message != eval.Message)
                        {
                            existingAlert.Message = eval.Message;
                            hasChange = true;
                        }

                        if (hasChange)
                        {
                            existingAlert.LastModifiedDate = DateTime.UtcNow;

                            if (GetSeverityPriority(eval.Severity) > GetSeverityPriority(oldSeverity))
                            {
                                existingAlert.Status = AlertConstants.Status.Open;
                                result.Actions.Add(LotQualityRecheckConstants.Action.Escalated);
                                notificationsToSend.Add((existingAlert, GetNotificationCode(eval.AlertType), eval.Message));
                            }
                            else
                            {
                                result.Actions.Add(LotQualityRecheckConstants.Action.Updated);
                            }

                            _context.Alerts.Update(existingAlert);
                            await _context.SaveChangesAsync(cancellationToken);
                            shouldNotifyRealtime = true;
                        }
                        else
                        {
                            result.Actions.Add(LotQualityRecheckConstants.Action.Unchanged);
                        }
                    }
                }
                else
                {
                    if (existingAlert != null)
                    {
                        existingAlert.Status = AlertConstants.Status.Resolved;
                        existingAlert.ResolvedAt = DateTime.UtcNow;
                        existingAlert.LastModifiedDate = DateTime.UtcNow;
                        existingAlert.DeduplicationKey = null;

                        _context.Alerts.Update(existingAlert);
                        await _context.SaveChangesAsync(cancellationToken);

                        result.Actions.Add(LotQualityRecheckConstants.Action.Resolved);
                        shouldNotifyRealtime = true;
                    }
                }
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            // Realtime SignalR — use already-tracked references instead of re-querying DB after commit
            if (shouldNotifyRealtime)
            {
                var resolvedAlerts = evaluations
                    .Where(e => !e.ShouldAlert)
                    .Select(e => activeAlertsForLot.FirstOrDefault(a => a.AlertType == e.AlertType && a.Status == AlertConstants.Status.Resolved))
                    .Where(a => a != null)
                    .Select(a => a!);

                var alertsToNotify = notificationsToSend.Select(x => x.Alert)
                    .Union(resolvedAlerts)
                    .Distinct()
                    .ToList();

                foreach (var alert in alertsToNotify)
                {
                    await NotifySignalRAsync(alert);
                }
            }

            // FCM dispatcher
            foreach (var (alert, code, msg) in notificationsToSend)
            {
                await SendNotificationAsync(code, msg, alert.Id);
            }
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

        return result;
    }

    private async Task<System.ValueTuple<LotQualityRecheckWarehouseConfig, List<string>>> GetWarehouseConfigAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var keys = new List<string>
        {
            LotQualityRecheckConstants.ConfigKey.IntervalDays,
            $"{LotQualityRecheckConstants.ConfigKey.IntervalDays}:{warehouseId}",
            LotQualityRecheckConstants.ConfigKey.OverdueCriticalDays,
            $"{LotQualityRecheckConstants.ConfigKey.OverdueCriticalDays}:{warehouseId}",
            LotQualityRecheckConstants.ConfigKey.MoistureWarning,
            $"{LotQualityRecheckConstants.ConfigKey.MoistureWarning}:{warehouseId}",
            LotQualityRecheckConstants.ConfigKey.MoistureCritical,
            $"{LotQualityRecheckConstants.ConfigKey.MoistureCritical}:{warehouseId}",
            LotQualityRecheckConstants.ConfigKey.MoldWarning,
            $"{LotQualityRecheckConstants.ConfigKey.MoldWarning}:{warehouseId}",
            LotQualityRecheckConstants.ConfigKey.MoldCritical,
            $"{LotQualityRecheckConstants.ConfigKey.MoldCritical}:{warehouseId}",
            LotQualityRecheckConstants.ConfigKey.LongStoredWarning,
            $"{LotQualityRecheckConstants.ConfigKey.LongStoredWarning}:{warehouseId}",
            LotQualityRecheckConstants.ConfigKey.LongStoredCritical,
            $"{LotQualityRecheckConstants.ConfigKey.LongStoredCritical}:{warehouseId}"
        };

        var configs = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => keys.Contains(c.ConfigKey) && !c.IsDeleted)
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, cancellationToken);

        int interval = TryGetInt(configs, $"{LotQualityRecheckConstants.ConfigKey.IntervalDays}:{warehouseId}",
            TryGetInt(configs, LotQualityRecheckConstants.ConfigKey.IntervalDays, 30));

        int overdueCritical = TryGetInt(configs, $"{LotQualityRecheckConstants.ConfigKey.OverdueCriticalDays}:{warehouseId}",
            TryGetInt(configs, LotQualityRecheckConstants.ConfigKey.OverdueCriticalDays, 7));

        decimal moistureWarning = TryGetDecimal(configs, $"{LotQualityRecheckConstants.ConfigKey.MoistureWarning}:{warehouseId}",
            TryGetDecimal(configs, LotQualityRecheckConstants.ConfigKey.MoistureWarning, 14.5m));

        decimal moistureCritical = TryGetDecimal(configs, $"{LotQualityRecheckConstants.ConfigKey.MoistureCritical}:{warehouseId}",
            TryGetDecimal(configs, LotQualityRecheckConstants.ConfigKey.MoistureCritical, 16.0m));

        string moldWarningStr = TryGetString(configs, $"{LotQualityRecheckConstants.ConfigKey.MoldWarning}:{warehouseId}",
            TryGetString(configs, LotQualityRecheckConstants.ConfigKey.MoldWarning, "NHE"));

        string moldCriticalStr = TryGetString(configs, $"{LotQualityRecheckConstants.ConfigKey.MoldCritical}:{warehouseId}",
            TryGetString(configs, LotQualityRecheckConstants.ConfigKey.MoldCritical, "NANG"));

        int longStoredWarning = TryGetInt(configs, $"{LotQualityRecheckConstants.ConfigKey.LongStoredWarning}:{warehouseId}",
            TryGetInt(configs, LotQualityRecheckConstants.ConfigKey.LongStoredWarning, 30));

        int longStoredCritical = TryGetInt(configs, $"{LotQualityRecheckConstants.ConfigKey.LongStoredCritical}:{warehouseId}",
            TryGetInt(configs, LotQualityRecheckConstants.ConfigKey.LongStoredCritical, 60));

        var config = new LotQualityRecheckWarehouseConfig
        {
            WarehouseId = warehouseId,
            InspectionIntervalDays = interval,
            InspectionOverdueCriticalDays = overdueCritical,
            MoistureWarningThreshold = moistureWarning,
            MoistureCriticalThreshold = moistureCritical,
            MoldWarningLevel = LotQualityRecheckHelper.ParseMoldLevel(moldWarningStr),
            MoldCriticalLevel = LotQualityRecheckHelper.ParseMoldLevel(moldCriticalStr),
            LongStoredWarningDays = longStoredWarning,
            LongStoredCriticalDays = longStoredCritical
        };

        var errors = new List<string>();
        if (interval <= 0) errors.Add(LotQualityRecheckConstants.ConfigKey.IntervalDays);
        if (overdueCritical <= 0) errors.Add(LotQualityRecheckConstants.ConfigKey.OverdueCriticalDays);
        if (moistureWarning <= 0) errors.Add(LotQualityRecheckConstants.ConfigKey.MoistureWarning);
        if (moistureCritical < moistureWarning) errors.Add(LotQualityRecheckConstants.ConfigKey.MoistureCritical);
        if (longStoredWarning <= 0) errors.Add(LotQualityRecheckConstants.ConfigKey.LongStoredWarning);
        if (longStoredCritical < longStoredWarning) errors.Add(LotQualityRecheckConstants.ConfigKey.LongStoredCritical);

        return (config, errors);
    }

    private async Task ManageConfigAlertAsync(int warehouseId, List<string> errors, CancellationToken cancellationToken)
    {
        var dedupKey = $"{LotQualityRecheckConstants.Job.DeduplicationKeyPrefix}_CONFIG:{warehouseId}";
        var activeConfigAlert = await _context.Alerts
            .FirstOrDefaultAsync(a => a.DeduplicationKey == dedupKey && !a.IsDeleted && a.Status != AlertConstants.Status.Resolved, cancellationToken);

        if (errors.Count > 0)
        {
            var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
            var warehouseName = warehouse?.Name ?? $"Kho {warehouseId}";
            var message = $"Cấu hình JOB-03 của kho {warehouseName} không hợp lệ hoặc thiếu: {string.Join(", ", errors)}.";

            if (activeConfigAlert == null)
            {
                var newAlert = new Alert
                {
                    AlertType = LotQualityRecheckConstants.AlertType.LotQualityRecheckConfigInvalid,
                    Severity = AlertConstants.Severity.Warning,
                    WarehouseId = warehouseId,
                    Message = message,
                    RelatedEntityType = AlertConstants.RelatedEntityType.Warehouse,
                    RelatedEntityId = warehouseId,
                    Status = AlertConstants.Status.Open,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false,
                    DeduplicationKey = dedupKey
                };
                await _context.Alerts.AddAsync(newAlert, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                await NotifySignalRAsync(newAlert);
                await SendNotificationAsync(NotificationConstants.Code.LotQualityRecheckConfigAlert, message, newAlert.Id);
            }
            else if (activeConfigAlert.Message != message)
            {
                activeConfigAlert.Message = message;
                activeConfigAlert.LastModifiedDate = DateTime.UtcNow;
                _context.Alerts.Update(activeConfigAlert);
                await _context.SaveChangesAsync(cancellationToken);

                await NotifySignalRAsync(activeConfigAlert);
                await SendNotificationAsync(NotificationConstants.Code.LotQualityRecheckConfigAlert, message, activeConfigAlert.Id);
            }
        }
        else
        {
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
        }
    }

    private string GetNotificationCode(string alertType)
    {
        return alertType switch
        {
            LotQualityRecheckConstants.AlertType.InspectionDue => NotificationConstants.Code.InspectionDueAlert,
            LotQualityRecheckConstants.AlertType.HighMoisture => NotificationConstants.Code.HighMoistureAlert,
            LotQualityRecheckConstants.AlertType.MouldRisk => NotificationConstants.Code.MouldRiskAlert,
            LotQualityRecheckConstants.AlertType.LongStoredLot => NotificationConstants.Code.LongStoredLotAlert,
            _ => NotificationConstants.Code.LotQualityRecheckConfigAlert
        };
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
            _logger.LogError(ex, "{LogPrefix} Realtime SignalR notification failed for alert {Id}.", LotQualityRecheckConstants.Job.LogPrefix, alert.Id);
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
                    CommonConstants.Role.OWNER,
                    CommonConstants.Role.WAREHOUSE
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
            _logger.LogError(ex, "{LogPrefix} Notification dispatcher failed for code {Code}.", LotQualityRecheckConstants.Job.LogPrefix, code);
        }
    }

    private int TryGetInt(Dictionary<string, string> dict, string key, int defaultValue)
    {
        if (dict.TryGetValue(key, out var val) && int.TryParse(val, out var res))
        {
            return res;
        }
        return defaultValue;
    }

    private decimal TryGetDecimal(Dictionary<string, string> dict, string key, decimal defaultValue)
    {
        if (dict.TryGetValue(key, out var val) && decimal.TryParse(val, NumberStyles.Number, CultureInfo.InvariantCulture, out var res))
        {
            return res;
        }
        return defaultValue;
    }

    private string TryGetString(Dictionary<string, string> dict, string key, string defaultValue)
    {
        if (dict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            return val;
        }
        return defaultValue;
    }
}
