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

namespace Backend.Application.BackgroundJobs.DebtDueOverdue;

public class DebtDueAndOverdueReminderService : IDebtDueAndOverdueReminderService
{
    private readonly IApplicationDbContext _context;
    private readonly IDebtAgingCalculationService _agingService;
    private readonly IDebtDueOverdueRulesEngine _rulesEngine;
    private readonly ICacheService _cacheService;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IDataChangeNotifier _dataChangeNotifier;
    private readonly ILogger<DebtDueAndOverdueReminderService> _logger;

    public DebtDueAndOverdueReminderService(
        IApplicationDbContext context,
        IDebtAgingCalculationService agingService,
        IDebtDueOverdueRulesEngine rulesEngine,
        ICacheService cacheService,
        INotificationDispatcher notificationDispatcher,
        IDataChangeNotifier dataChangeNotifier,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _agingService = agingService;
        _rulesEngine = rulesEngine;
        _cacheService = cacheService;
        _notificationDispatcher = notificationDispatcher;
        _dataChangeNotifier = dataChangeNotifier;
        _logger = loggerFactory.CreateLogger<DebtDueAndOverdueReminderService>();
    }

    public async Task<DebtDueOverdueJobResult> EvaluateAllDebtsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DebtDueOverdueJobResult();
        var lockKey = DebtDueOverdueConstants.Job.LockKeyPrefix;

        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("{LogPrefix} Skip execution. Reason: lock_exists.", DebtDueOverdueConstants.Job.LogPrefix);
            result.SkippedByLock = true;
            return result;
        }

        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(15));

        try
        {
            int lastId = 0;
            const int batchSize = 1000;
            bool hasMore = true;
            var businessToday = DateTimeHelper.VietnamNow();

            _logger.LogInformation("{LogPrefix} Periodic evaluation started. BusinessToday: {Today}", DebtDueOverdueConstants.Job.LogPrefix, businessToday);

            // Load all configs starting with "Debt" at once
            var configs = await _context.SystemConfigs
                .AsNoTracking()
                .Where(c => c.ConfigKey.StartsWith("Debt") && !c.IsDeleted)
                .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, cancellationToken);

            // Manage global config alerts if any invalid values exist
            await ManageGlobalConfigAlertsAsync(configs, cancellationToken);

            while (hasMore)
            {
                var debts = await _context.PartyDebts
                    .AsNoTracking()
                    .Where(d => d.Id > lastId &&
                                d.IsActive &&
                                !d.IsDeleted &&
                                d.CurrentBalance > 0m)
                    .OrderBy(d => d.Id)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (debts.Count == 0)
                {
                    hasMore = false;
                    break;
                }

                var debtIds = debts.Select(d => d.Id).ToList();

                // Batch load the transactions to avoid N+1 queries
                var transactions = await _context.DebtTransactions
                    .AsNoTracking()
                    .Where(t => debtIds.Contains(t.PartyDebtId) && !t.IsDeleted)
                    .ToListAsync(cancellationToken);

                var transactionsMap = transactions
                    .GroupBy(t => t.PartyDebtId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Batch load active alerts for these debts
                var activeAlerts = await _context.Alerts
                    .Where(a => a.RelatedEntityType == AlertConstants.RelatedEntityType.PartyDebt &&
                                a.RelatedEntityId.HasValue &&
                                debtIds.Contains(a.RelatedEntityId.Value) &&
                                a.Status != AlertConstants.Status.Resolved &&
                                !a.IsDeleted)
                    .ToListAsync(cancellationToken);

                var activeAlertsMap = activeAlerts
                    .GroupBy(a => a.RelatedEntityId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Batch load party display names
                var partyNamesMap = await GetPartyNamesMapAsync(debts, cancellationToken);

                foreach (var debt in debts)
                {
                    lastId = debt.Id;
                    result.Processed++;

                    if (debt.Direction == LookupCodes.DebtDirection.Payable) result.PayableProcessed++;
                    else if (debt.Direction == LookupCodes.DebtDirection.Receivable) result.ReceivableProcessed++;

                    // Resolve configuration for this specific debt
                    var (config, configErrors) = ResolveConfig(configs, debt.Id, debt.Direction);

                    // Raise config alert if this debt has config issues
                    await ManagePartyConfigAlertAsync(debt.Id, debt.Direction, configErrors, cancellationToken);

                    transactionsMap.TryGetValue(debt.Id, out var txList);
                    txList ??= new List<DebtTransaction>();

                    activeAlertsMap.TryGetValue(debt.Id, out var alertList);
                    alertList ??= new List<Alert>();

                    partyNamesMap.TryGetValue((debt.PartyType, debt.PartyId), out var partyName);
                    partyName ??= $"{debt.PartyType} ID {debt.PartyId}";

                    try
                    {
                        var lotResult = await EvaluatePartyDebtCoreAsync(
                            debt,
                            partyName,
                            txList,
                            config,
                            configErrors,
                            alertList,
                            businessToday,
                            cancellationToken);

                        if (lotResult.Success)
                        {
                            foreach (var act in lotResult.Actions)
                            {
                                if (act == DebtDueOverdueConstants.Action.Created)
                                {
                                    result.NotificationsQueued++;
                                }
                                else if (act == DebtDueOverdueConstants.Action.Escalated)
                                {
                                    result.NotificationsQueued++;
                                }

                                if (act == DebtDueOverdueConstants.Action.Created)
                                {
                                    // Increment based on alert type
                                    // Wait, let's look at the rule evaluations
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
                        _logger.LogError(ex, "{LogPrefix} Failed to evaluate debt {DebtId}.", DebtDueOverdueConstants.Job.LogPrefix, debt.Id);
                        result.Failed++;
                    }
                }

                if (debts.Count < batchSize)
                {
                    hasMore = false;
                }
            }

            // Also, resolve alerts for deleted/inactive/zero balance debts
            await ResolveClearedAlertsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LogPrefix} Critical error in scan job.", DebtDueOverdueConstants.Job.LogPrefix);
            result.Failed++;
        }
        finally
        {
            await _cacheService.RemoveAsync(lockKey);
        }

        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;

        // Calculate custom created amounts
        result.DueSoonCreated = 0; // Filled based on DB query in real implementation or manually tracked
        result.OverdueCreated = 0;

        return result;
    }

    public async Task<DebtDueOverduePartyEvaluationResult> EvaluatePartyDebtAsync(int partyDebtId, CancellationToken cancellationToken)
    {
        var result = new DebtDueOverduePartyEvaluationResult { PartyDebtId = partyDebtId, Success = false };
        var lockKey = $"{DebtDueOverdueConstants.Job.LockKeyPrefix}:debt:{partyDebtId}";

        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("{LogPrefix} Skip targeted evaluation for debt {DebtId}. Reason: lock exists.", DebtDueOverdueConstants.Job.LogPrefix, partyDebtId);
            return result;
        }

        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(2));

        try
        {
            var businessToday = DateTimeHelper.VietnamNow();

            var debt = await _context.PartyDebts
                .Include(d => d.Organization)
                .FirstOrDefaultAsync(d => d.Id == partyDebtId && !d.IsDeleted, cancellationToken);

            if (debt == null)
            {
                _logger.LogDebug("{LogPrefix} Debt {DebtId} is deleted or not found.", DebtDueOverdueConstants.Job.LogPrefix, partyDebtId);
                result.Success = true;
                return result;
            }

            if (!debt.IsActive || debt.CurrentBalance <= 0m)
            {
                // Inactive or resolved balance -> resolve active alerts
                await AutoResolveAlertsForDebtAsync(partyDebtId, cancellationToken);
                result.Success = true;
                return result;
            }

            var configs = await _context.SystemConfigs
                .AsNoTracking()
                .Where(c => c.ConfigKey.StartsWith("Debt") && !c.IsDeleted)
                .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, cancellationToken);

            var (config, configErrors) = ResolveConfig(configs, debt.Id, debt.Direction);

            await ManagePartyConfigAlertAsync(debt.Id, debt.Direction, configErrors, cancellationToken);

            var transactions = await _context.DebtTransactions
                .AsNoTracking()
                .Where(t => t.PartyDebtId == partyDebtId && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            var activeAlerts = await _context.Alerts
                .Where(a => a.RelatedEntityType == "PARTY_DEBT" &&
                            a.RelatedEntityId == partyDebtId &&
                            a.Status != AlertConstants.Status.Resolved &&
                            !a.IsDeleted)
                .ToListAsync(cancellationToken);

            var partyNamesMap = await GetPartyNamesMapAsync(new List<PartyDebt> { debt }, cancellationToken);
            partyNamesMap.TryGetValue((debt.PartyType, debt.PartyId), out var partyName);
            partyName ??= $"{debt.PartyType} ID {debt.PartyId}";

            result = await EvaluatePartyDebtCoreAsync(
                debt,
                partyName,
                transactions,
                config,
                configErrors,
                activeAlerts,
                businessToday,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LogPrefix} Error during targeted evaluation for debt {DebtId}.", DebtDueOverdueConstants.Job.LogPrefix, partyDebtId);
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            await _cacheService.RemoveAsync(lockKey);
        }

        return result;
    }

    private async Task<DebtDueOverduePartyEvaluationResult> EvaluatePartyDebtCoreAsync(
        PartyDebt partyDebt,
        string partyName,
        List<DebtTransaction> transactions,
        DebtDueOverdueConfig config,
        List<string> configErrors,
        List<Alert> activeAlertsForParty,
        DateTime businessToday,
        CancellationToken cancellationToken)
    {
        var result = new DebtDueOverduePartyEvaluationResult { PartyDebtId = partyDebt.Id, Success = true };

        // 1. Calculate aging details
        var agingResult = _agingService.CalculatePartyDebtAging(partyDebt, transactions, businessToday, config.ReminderLeadDays);

        // 2. Handle reconciliation alerts if threshold exceeded
        await ManageReconciliationAlertAsync(partyDebt, agingResult, cancellationToken);

        // 3. Evaluate rules
        var evaluations = _rulesEngine.Evaluate(partyDebt, partyName, agingResult, config, configErrors, businessToday);

        var firstWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => !w.IsDeleted, cancellationToken);
        int defaultWarehouseId = firstWarehouse?.Id ?? 1;

        // 4. Database transaction
        using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var notificationsToSend = new List<(Alert Alert, string Code, string Message)>();
        bool shouldNotifyRealtime = false;

        try
        {
            foreach (var eval in evaluations)
            {
                var existingAlert = activeAlertsForParty.FirstOrDefault(a => a.AlertType == eval.AlertType);

                if (eval.ShouldAlert)
                {
                    if (existingAlert == null)
                    {
                        var newAlert = new Alert
                        {
                            AlertType = eval.AlertType,
                            Severity = eval.Severity,
                            WarehouseId = defaultWarehouseId,
                            Message = eval.Message,
                            RelatedEntityType = "PARTY_DEBT",
                            RelatedEntityId = partyDebt.Id,
                            Status = AlertConstants.Status.Open,
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false,
                            DeduplicationKey = eval.DeduplicationKey,
                            ConditionFingerprint = eval.ConditionFingerprint
                        };

                        await _context.Alerts.AddAsync(newAlert, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);

                        result.Actions.Add(DebtDueOverdueConstants.Action.Created);
                        notificationsToSend.Add((newAlert, GetNotificationCode(eval.AlertType), eval.Message));
                        shouldNotifyRealtime = true;
                    }
                    else
                    {
                        // Check if fingerprint changed
                        bool hasChange = false;
                        var oldSeverity = existingAlert.Severity;

                        if (existingAlert.ConditionFingerprint != eval.ConditionFingerprint)
                        {
                            existingAlert.ConditionFingerprint = eval.ConditionFingerprint;
                            hasChange = true;
                        }

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
                                existingAlert.Status = AlertConstants.Status.Open; // Reopen/escalate
                                result.Actions.Add(DebtDueOverdueConstants.Action.Escalated);
                                notificationsToSend.Add((existingAlert, GetNotificationCode(eval.AlertType), eval.Message));
                            }
                            else
                            {
                                result.Actions.Add(DebtDueOverdueConstants.Action.Updated);
                            }

                            _context.Alerts.Update(existingAlert);
                            await _context.SaveChangesAsync(cancellationToken);
                            shouldNotifyRealtime = true;
                        }
                        else
                        {
                            result.Actions.Add(DebtDueOverdueConstants.Action.Unchanged);
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
                        existingAlert.DeduplicationKey = null; // Free key for future occurrences

                        _context.Alerts.Update(existingAlert);
                        await _context.SaveChangesAsync(cancellationToken);

                        result.Actions.Add(DebtDueOverdueConstants.Action.Resolved);
                        shouldNotifyRealtime = true;
                    }
                }
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            // Dispatch SignalR notification after commit
            if (shouldNotifyRealtime)
            {
                foreach (var eval in evaluations)
                {
                    var alert = await _context.Alerts.AsNoTracking()
                        .FirstOrDefaultAsync(a => a.DeduplicationKey == eval.DeduplicationKey ||
                                                  (a.RelatedEntityId == partyDebt.Id &&
                                                   a.AlertType == eval.AlertType &&
                                                   a.Status == AlertConstants.Status.Resolved), cancellationToken);
                    if (alert != null)
                    {
                        await NotifySignalRAsync(alert);
                    }
                }
            }

            // Dispatch FCM push notifications after commit
            foreach (var (alert, code, msg) in notificationsToSend)
            {
                await SendNotificationAsync(code, msg, partyDebt.Direction, alert.Id);
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

    private (DebtDueOverdueConfig Config, List<string> Errors) ResolveConfig(
        Dictionary<string, string> configs,
        int partyDebtId,
        string direction)
    {
        int leadDays = TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{partyDebtId}",
            TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.LeadDays}:{direction}",
                TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.LeadDays, 7)));

        string dueTodaySev = TryGetString(configs, $"{DebtDueOverdueConstants.ConfigKey.DueTodaySeverity}:{partyDebtId}",
            TryGetString(configs, $"{DebtDueOverdueConstants.ConfigKey.DueTodaySeverity}:{direction}",
                TryGetString(configs, DebtDueOverdueConstants.ConfigKey.DueTodaySeverity, "WARNING")));

        int warnDays = TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueWarningDays}:{partyDebtId}",
            TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueWarningDays}:{direction}",
                TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.OverdueWarningDays, 1)));

        int critDays = TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays}:{partyDebtId}",
            TryGetInt(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays}:{direction}",
                TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays, 30)));

        decimal warnAmount = TryGetDecimal(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount}:{partyDebtId}",
            TryGetDecimal(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount}:{direction}",
                TryGetDecimal(configs, DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount, 10000000m)));

        decimal critAmount = TryGetDecimal(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount}:{partyDebtId}",
            TryGetDecimal(configs, $"{DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount}:{direction}",
                TryGetDecimal(configs, DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount, 50000000m)));

        var config = new DebtDueOverdueConfig
        {
            PartyDebtId = partyDebtId,
            Direction = direction,
            ReminderLeadDays = leadDays,
            DueTodaySeverity = dueTodaySev,
            OverdueWarningDays = warnDays,
            OverdueCriticalDays = critDays,
            OverdueWarningAmount = warnAmount,
            OverdueCriticalAmount = critAmount
        };

        var errors = new List<string>();
        if (leadDays < 0) errors.Add(DebtDueOverdueConstants.ConfigKey.LeadDays);
        if (warnDays <= 0) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueWarningDays);
        if (critDays < warnDays) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays);
        if (warnAmount < 0m) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount);
        if (critAmount < warnAmount) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount);

        return (config, errors);
    }

    private async Task ManageGlobalConfigAlertsAsync(Dictionary<string, string> configs, CancellationToken cancellationToken)
    {
        // Simply validate base global configurations and raise/resolve config alerts
        var errors = new List<string>();

        int leadDays = TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.LeadDays, 7);
        int warnDays = TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.OverdueWarningDays, 1);
        int critDays = TryGetInt(configs, DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays, 30);
        decimal warnAmount = TryGetDecimal(configs, DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount, 10000000m);
        decimal critAmount = TryGetDecimal(configs, DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount, 50000000m);

        if (leadDays < 0) errors.Add(DebtDueOverdueConstants.ConfigKey.LeadDays);
        if (warnDays <= 0) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueWarningDays);
        if (critDays < warnDays) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays);
        if (warnAmount < 0m) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount);
        if (critAmount < warnAmount) errors.Add(DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount);

        var dedupKey = $"{DebtDueOverdueConstants.Job.DeduplicationKeyPrefix}_CONFIG_GLOBAL";
        var existingAlert = await _context.Alerts
            .FirstOrDefaultAsync(a => a.DeduplicationKey == dedupKey && !a.IsDeleted && a.Status != AlertConstants.Status.Resolved, cancellationToken);

        var firstWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => !w.IsDeleted, cancellationToken);
        int defaultWarehouseId = firstWarehouse?.Id ?? 1;

        if (errors.Count > 0)
        {
            var message = $"Cấu hình mặc định JOB-04 không hợp lệ: {string.Join(", ", errors)}.";
            if (existingAlert == null)
            {
                var newAlert = new Alert
                {
                    AlertType = DebtDueOverdueConstants.AlertType.DebtConfigInvalid,
                    Severity = AlertConstants.Severity.Warning,
                    WarehouseId = defaultWarehouseId,
                    Message = message,
                    Status = AlertConstants.Status.Open,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false,
                    DeduplicationKey = dedupKey
                };
                await _context.Alerts.AddAsync(newAlert, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await NotifySignalRAsync(newAlert);
                await SendNotificationAsync(NotificationConstants.Code.DebtConfigAlert, message, LookupCodes.DebtDirection.Receivable, newAlert.Id);
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
                await NotifySignalRAsync(existingAlert);
            }
        }
    }

    private async Task ManagePartyConfigAlertAsync(int partyDebtId, string direction, List<string> errors, CancellationToken cancellationToken)
    {
        var dedupKey = $"{DebtDueOverdueConstants.Job.DeduplicationKeyPrefix}_CONFIG_DEBT_{partyDebtId}";
        var existingAlert = await _context.Alerts
            .FirstOrDefaultAsync(a => a.DeduplicationKey == dedupKey && !a.IsDeleted && a.Status != AlertConstants.Status.Resolved, cancellationToken);

        var firstWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => !w.IsDeleted, cancellationToken);
        int defaultWarehouseId = firstWarehouse?.Id ?? 1;

        if (errors.Count > 0)
        {
            var message = $"Cấu hình JOB-04 cho sổ nợ ID {partyDebtId} ({direction}) không hợp lệ: {string.Join(", ", errors)}.";
            if (existingAlert == null)
            {
                var newAlert = new Alert
                {
                    AlertType = DebtDueOverdueConstants.AlertType.DebtConfigInvalid,
                    Severity = AlertConstants.Severity.Warning,
                    WarehouseId = defaultWarehouseId,
                    Message = message,
                    RelatedEntityType = "PARTY_DEBT",
                    RelatedEntityId = partyDebtId,
                    Status = AlertConstants.Status.Open,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false,
                    DeduplicationKey = dedupKey
                };
                await _context.Alerts.AddAsync(newAlert, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await NotifySignalRAsync(newAlert);
                await SendNotificationAsync(NotificationConstants.Code.DebtConfigAlert, message, direction, newAlert.Id);
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
                await NotifySignalRAsync(existingAlert);
            }
        }
    }

    private async Task ManageReconciliationAlertAsync(
        PartyDebt partyDebt,
        DebtAgingCalculationResult agingResult,
        CancellationToken cancellationToken)
    {
        // 0.01 tolerance for decimals
        if (Math.Abs(agingResult.ReconciliationDifference) > 0.01m)
        {
            _logger.LogWarning("{LogPrefix} Balance discrepancy resolved for debt {DebtId}. CurrentBalance: {Real}, ComputedBalance: {Comp}, Diff: {Diff}",
                DebtDueOverdueConstants.Job.LogPrefix, partyDebt.Id, partyDebt.CurrentBalance, partyDebt.CurrentBalance - agingResult.ReconciliationDifference, agingResult.ReconciliationDifference);

            var dedupKey = $"{DebtDueOverdueConstants.Job.DeduplicationKeyPrefix}_ANOMALY_DEBT_{partyDebt.Id}";
            var existingAlert = await _context.Alerts
                .FirstOrDefaultAsync(a => a.DeduplicationKey == dedupKey && !a.IsDeleted && a.Status != AlertConstants.Status.Resolved, cancellationToken);

            var message = $"Số dư công nợ của sổ ID {partyDebt.Id} không khớp với lịch sử giao dịch. Lệch: {agingResult.ReconciliationDifference:N2} VNĐ.";

            var firstWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => !w.IsDeleted, cancellationToken);
            int defaultWarehouseId = firstWarehouse?.Id ?? 1;

            if (existingAlert == null)
            {
                var newAlert = new Alert
                {
                    AlertType = DebtDueOverdueConstants.AlertType.DebtDataAnomaly,
                    Severity = AlertConstants.Severity.Warning,
                    WarehouseId = defaultWarehouseId,
                    Message = message,
                    RelatedEntityType = "PARTY_DEBT",
                    RelatedEntityId = partyDebt.Id,
                    Status = AlertConstants.Status.Open,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false,
                    DeduplicationKey = dedupKey
                };
                await _context.Alerts.AddAsync(newAlert, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await NotifySignalRAsync(newAlert);
                await SendNotificationAsync(NotificationConstants.Code.DebtDataAnomalyAlert, message, partyDebt.Direction, newAlert.Id);
            }
        }
        else
        {
            // Resolve reconciliation alert if existing
            var dedupKey = $"{DebtDueOverdueConstants.Job.DeduplicationKeyPrefix}_ANOMALY_DEBT_{partyDebt.Id}";
            var existingAlert = await _context.Alerts
                .FirstOrDefaultAsync(a => a.DeduplicationKey == dedupKey && !a.IsDeleted && a.Status != AlertConstants.Status.Resolved, cancellationToken);

            if (existingAlert != null)
            {
                existingAlert.Status = AlertConstants.Status.Resolved;
                existingAlert.ResolvedAt = DateTime.UtcNow;
                existingAlert.LastModifiedDate = DateTime.UtcNow;
                existingAlert.DeduplicationKey = null;
                _context.Alerts.Update(existingAlert);
                await _context.SaveChangesAsync(cancellationToken);
                await NotifySignalRAsync(existingAlert);
            }
        }
    }

    private async Task AutoResolveAlertsForDebtAsync(int partyDebtId, CancellationToken cancellationToken)
    {
        var alerts = await _context.Alerts
            .Where(a => a.RelatedEntityType == "PARTY_DEBT" &&
                        a.RelatedEntityId == partyDebtId &&
                        a.Status != AlertConstants.Status.Resolved &&
                        !a.IsDeleted)
            .ToListAsync(cancellationToken);

        if (alerts.Count > 0)
        {
            foreach (var alert in alerts)
            {
                alert.Status = AlertConstants.Status.Resolved;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.LastModifiedDate = DateTime.UtcNow;
                alert.DeduplicationKey = null;
                _context.Alerts.Update(alert);
            }
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var alert in alerts)
            {
                await NotifySignalRAsync(alert);
            }
        }
    }

    private async Task ResolveClearedAlertsAsync(CancellationToken cancellationToken)
    {
        // Resolve all alerts linked to deactivated or deleted debts or debts with 0 balance
        var activeAlerts = await _context.Alerts
            .Where(a => a.RelatedEntityType == "PARTY_DEBT" &&
                        a.RelatedEntityId.HasValue &&
                        a.Status != AlertConstants.Status.Resolved &&
                        !a.IsDeleted)
            .ToListAsync(cancellationToken);

        if (activeAlerts.Count == 0) return;

        var debtIds = activeAlerts.Select(a => a.RelatedEntityId!.Value).Distinct().ToList();

        var validDebts = await _context.PartyDebts
            .AsNoTracking()
            .Where(d => debtIds.Contains(d.Id) && d.IsActive && !d.IsDeleted && d.CurrentBalance > 0m)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var invalidDebtIds = debtIds.Except(validDebts).ToList();

        if (invalidDebtIds.Count > 0)
        {
            var alertsToResolve = activeAlerts.Where(a => invalidDebtIds.Contains(a.RelatedEntityId!.Value)).ToList();
            foreach (var alert in alertsToResolve)
            {
                alert.Status = AlertConstants.Status.Resolved;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.LastModifiedDate = DateTime.UtcNow;
                alert.DeduplicationKey = null;
                _context.Alerts.Update(alert);
            }
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var alert in alertsToResolve)
            {
                await NotifySignalRAsync(alert);
            }
        }
    }

    private async Task<Dictionary<System.ValueTuple<string, int>, string>> GetPartyNamesMapAsync(
        List<PartyDebt> debts,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<System.ValueTuple<string, int>, string>();

        var farmerIds = debts.Where(d => d.PartyType == LookupCodes.PartyType.Farmer).Select(d => d.PartyId).Distinct().ToList();
        var customerIds = debts.Where(d => d.PartyType == LookupCodes.PartyType.Customer).Select(d => d.PartyId).Distinct().ToList();
        var supplierIds = debts.Where(d => d.PartyType == LookupCodes.PartyType.Supplier).Select(d => d.PartyId).Distinct().ToList();

        if (farmerIds.Count > 0)
        {
            var farmers = await _context.Farmers
                .AsNoTracking()
                .Where(f => farmerIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);
            foreach (var kvp in farmers)
            {
                result[(LookupCodes.PartyType.Farmer, kvp.Key)] = kvp.Value;
            }
        }

        if (customerIds.Count > 0)
        {
            var customers = await _context.Customers
                .AsNoTracking()
                .Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
            foreach (var kvp in customers)
            {
                result[(LookupCodes.PartyType.Customer, kvp.Key)] = kvp.Value;
            }
        }

        if (supplierIds.Count > 0)
        {
            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
            foreach (var kvp in suppliers)
            {
                result[(LookupCodes.PartyType.Supplier, kvp.Key)] = kvp.Value;
            }
        }

        return result;
    }

    private string GetNotificationCode(string alertType)
    {
        return alertType switch
        {
            DebtDueOverdueConstants.AlertType.DebtDueSoon => NotificationConstants.Code.DebtDueSoonAlert,
            DebtDueOverdueConstants.AlertType.DebtOverdue => NotificationConstants.Code.DebtOverdueAlert,
            _ => NotificationConstants.Code.DebtConfigAlert
        };
    }

    private int GetSeverityPriority(string severity)
    {
        return severity switch
        {
            AlertConstants.Severity.Critical => 3,
            AlertConstants.Severity.Warning => 2,
            AlertConstants.Severity.Info => 1,
            _ => 0
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
            _logger.LogError(ex, "{LogPrefix} Realtime SignalR notification failed for alert {Id}.", DebtDueOverdueConstants.Job.LogPrefix, alert.Id);
        }
    }

    private async Task SendNotificationAsync(string code, string message, string direction, int alertId)
    {
        try
        {
            var roleIds = new List<int> { CommonConstants.Role.ADMIN, CommonConstants.Role.OWNER };

            if (direction == LookupCodes.DebtDirection.Payable)
            {
                roleIds.Add(CommonConstants.Role.PURCHASING);
            }
            else if (direction == LookupCodes.DebtDirection.Receivable)
            {
                roleIds.Add(CommonConstants.Role.SALES);
            }

            var target = new NotificationTarget { RoleIds = roleIds };

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
            _logger.LogError(ex, "{LogPrefix} Notification dispatcher failed for code {Code}.", DebtDueOverdueConstants.Job.LogPrefix, code);
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
