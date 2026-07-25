using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backend.Application.BackgroundJobs.FcmNotificationRetry;

public class FcmNotificationRetryService : IFcmNotificationRetryService
{
    private readonly IApplicationDbContext _context;
    private readonly IFcmFailureClassifier _classifier;
    private readonly ICacheService _cacheService;
    private readonly IFcmClient _fcmClient;
    private readonly ILogger<FcmNotificationRetryService> _logger;

    public FcmNotificationRetryService(
        IApplicationDbContext context,
        IFcmFailureClassifier classifier,
        ICacheService cacheService,
        IFcmClient fcmClient,
        ILogger<FcmNotificationRetryService> logger)
    {
        _context = context;
        _classifier = classifier;
        _cacheService = cacheService;
        _fcmClient = fcmClient;
        _logger = logger;
    }

    public async Task<FcmNotificationRetryResult> RunRetryJobAsync(CancellationToken cancellationToken)
    {
        var result = new FcmNotificationRetryResult();
        var lockKey = FcmNotificationRetryConstants.Job.LockKeyPrefix;

        // 1. Distributed Lock
        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("[JOB-05] Skip execution. Reason: lock_exists (Job is already running).");
            result.SkippedByLock = true;
            return result;
        }

        // Acquire lock for 10 minutes
        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(10));

        var runId = Guid.NewGuid().ToString("N")[..8];
        var startedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "[JOB-05] FCM Notification Retry Job started. runId={RunId}, schedule='*/2 * * * *', batchSize={BatchSize}, maxAttempts={MaxAttempts}, startedAt={StartedAt}",
            runId, FcmNotificationRetryConstants.DefaultOptions.BatchSize, FcmNotificationRetryConstants.DefaultOptions.MaxAttempts, startedAt);

        try
        {
            // 2. Recovery Phase: Recover stuck processing logs
            await RecoverStuckLeasesAsync(result, cancellationToken);

            // 3. Query Phase: Retrieve eligible logs
            var eligibleLogs = await QueryEligibleLogsAsync(result, cancellationToken);
            if (eligibleLogs.Count == 0)
            {
                _logger.LogInformation("[JOB-05] No eligible logs found to retry. runId={RunId}", runId);
                return result;
            }

            // 4. Claim Phase: Atomically claim rows under a short transaction
            var claimedLogs = await ClaimLogsAsync(eligibleLogs, runId, result, cancellationToken);
            if (claimedLogs.Count == 0)
            {
                _logger.LogInformation("[JOB-05] Failed to claim any logs in this batch. runId={RunId}", runId);
                return result;
            }

            // 5. Deliver Phase: Call FCM provider outside transaction with bounded parallelism
            var deliveryOutcomes = await DeliverBatchAsync(claimedLogs, result, cancellationToken);

            // 6. Finalize Phase: Persist outcomes in a short transaction
            await FinalizeBatchAsync(deliveryOutcomes, runId, result, cancellationToken);

            // Check remaining backlog
            var remainingBacklog = await _context.FcmNotificationLogs
                .CountAsync(x => !x.IsSent &&
                                 !x.IsDeleted &&
                                 (x.Status == FcmNotificationRetryConstants.Status.Pending || x.Status == FcmNotificationRetryConstants.Status.RetryScheduled) &&
                                 x.AttemptCount < FcmNotificationRetryConstants.DefaultOptions.MaxAttempts,
                            cancellationToken);

            var oldestPending = await _context.FcmNotificationLogs
                .Where(x => !x.IsSent &&
                             !x.IsDeleted &&
                             (x.Status == FcmNotificationRetryConstants.Status.Pending || x.Status == FcmNotificationRetryConstants.Status.RetryScheduled))
                .OrderBy(x => x.CreatedDate)
                .Select(x => (DateTime?)x.CreatedDate)
                .FirstOrDefaultAsync(cancellationToken);

            var oldestPendingAgeMinutes = oldestPending.HasValue ? (int)(DateTime.UtcNow - oldestPending.Value).TotalMinutes : 0;

            _logger.LogInformation(
                "[JOB-05] Run completed. runId={RunId}, eligible={Eligible}, claimed={Claimed}, processed={Processed}, sent={Sent}, retryScheduled={RetryScheduled}, permanentFailed={PermanentFailed}, invalidTokens={InvalidTokens}, devicesRevoked={DevicesRevoked}, exhausted={Exhausted}, stale={Stale}, payloadInvalid={PayloadInvalid}, leaseRecovered={LeaseRecovered}, skipped={Skipped}, failed={Failed}, backlogRemaining={BacklogRemaining}, oldestPendingAgeMinutes={OldestPendingAgeMinutes}, durationMs={DurationMs}",
                runId, result.Eligible, result.Claimed, result.Processed, result.Sent, result.RetryScheduled, result.PermanentFailed, result.InvalidTokens, result.DevicesRevoked, result.Exhausted, result.Stale, result.PayloadInvalid, result.LeaseRecovered, result.Skipped, result.Failed, remainingBacklog, oldestPendingAgeMinutes, (int)(DateTime.UtcNow - startedAt).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-05] Critical failure during execution of recurring job run. runId={RunId}", runId);
            result.Failed++;
        }
        finally
        {
            await _cacheService.RemoveAsync(lockKey);
        }

        result.DurationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
        return result;
    }

    private async Task RecoverStuckLeasesAsync(FcmNotificationRetryResult result, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var stuckLogs = await _context.FcmNotificationLogs
            .Where(x => x.Status == FcmNotificationRetryConstants.Status.Processing &&
                        x.ProcessingLeaseUntil.HasValue &&
                        x.ProcessingLeaseUntil.Value < now)
            .ToListAsync(cancellationToken);

        if (stuckLogs.Count > 0)
        {
            _logger.LogWarning("[JOB-05] Found {Count} logs stuck in PROCESSING state with expired leases. Recovering...", stuckLogs.Count);

            foreach (var log in stuckLogs)
            {
                log.Status = FcmNotificationRetryConstants.Status.RetryScheduled;
                log.ProcessingStartedAt = null;
                log.ProcessingLeaseUntil = null;
                log.ProcessingBy = null;
                log.LastModifiedDate = now;

                _logger.LogInformation(
                    "[JOB-05] Log lease recovered. notificationLogId={LogId}, action=PROCESSING_LEASE_RECOVERED, previousWorker={Worker}",
                    log.Id, log.ProcessingBy);

                result.LeaseRecovered++;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<FcmNotificationLog>> QueryEligibleLogsAsync(FcmNotificationRetryResult result, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = _context.FcmNotificationLogs
            .Where(x => !x.IsSent &&
                        !x.IsDeleted &&
                        (x.Status == FcmNotificationRetryConstants.Status.Pending ||
                         x.Status == FcmNotificationRetryConstants.Status.RetryScheduled) &&
                        x.AttemptCount < FcmNotificationRetryConstants.DefaultOptions.MaxAttempts &&
                        (x.NextRetryAt == null || x.NextRetryAt.Value <= now) &&
                        (x.ProcessingLeaseUntil == null || x.ProcessingLeaseUntil.Value < now));

        result.Eligible = await query.CountAsync(cancellationToken);

        return await query
            .OrderBy(x => x.NextRetryAt)
            .ThenBy(x => x.CreatedDate)
            .ThenBy(x => x.Id)
            .Take(FcmNotificationRetryConstants.DefaultOptions.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<FcmNotificationLog>> ClaimLogsAsync(
        List<FcmNotificationLog> eligibleLogs,
        string runId,
        FcmNotificationRetryResult result,
        CancellationToken cancellationToken)
    {
        var claimed = new List<FcmNotificationLog>();
        var now = DateTime.UtcNow;

        using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // Reload and lock rows for update if provider supports it, or use standard EF optimistic claim
            var logIds = eligibleLogs.Select(x => x.Id).ToList();

            var dbLogs = await _context.FcmNotificationLogs
                .Where(x => logIds.Contains(x.Id) &&
                            !x.IsSent &&
                            !x.IsDeleted &&
                            (x.Status == FcmNotificationRetryConstants.Status.Pending ||
                             x.Status == FcmNotificationRetryConstants.Status.RetryScheduled))
                .ToListAsync(cancellationToken);

            foreach (var log in dbLogs)
            {
                log.Status = FcmNotificationRetryConstants.Status.Processing;
                log.ProcessingStartedAt = now;
                log.ProcessingLeaseUntil = now.AddMinutes(FcmNotificationRetryConstants.DefaultOptions.ProcessingLeaseDurationMinutes);
                log.ProcessingBy = runId;
                log.LastModifiedDate = now;

                claimed.Add(log);
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            result.Claimed = claimed.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-05] Concurrency error claiming records for batch execution. Rolling back. runId={RunId}", runId);
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            // Clear tracking state so they are not finalized erroneously
            claimed.Clear();
        }

        return claimed;
    }

    private async Task<List<DeliveryOutcome>> DeliverBatchAsync(
        List<FcmNotificationLog> claimedLogs,
        FcmNotificationRetryResult result,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<DeliveryOutcome>();

        // Load devices in a batch to avoid N+1 queries
        var deviceIds = claimedLogs.Where(l => l.UserDeviceId.HasValue).Select(l => l.UserDeviceId!.Value).Distinct().ToList();
        var devices = await _context.UserDevices
            .Where(x => deviceIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // Load referenced alerts in a batch to inspect state validity
        var alertIds = claimedLogs.Where(l => l.ReferenceType == NotificationConstants.ReferenceType.Alert && l.ReferenceId.HasValue).Select(l => l.ReferenceId!.Value).Distinct().ToList();
        var alerts = await _context.Alerts
            .Where(x => alertIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // Limit parallelism to avoid overwhelming external client SDK or CPU
        using var semaphore = new SemaphoreSlim(10);
        var tasks = claimedLogs.Select(async log =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var outcome = await DeliverSingleLogAsync(log, devices, alerts, cancellationToken);
                lock (outcomes)
                {
                    outcomes.Add(outcome);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOB-05] Unexpected exception during delivery loop. notificationLogId={LogId}", log.Id);
                lock (outcomes)
                {
                    outcomes.Add(new DeliveryOutcome
                    {
                        LogId = log.Id,
                        Classification = FcmNotificationRetryConstants.Outcome.Unknown,
                        ErrorMessage = ex.Message,
                        NewAttemptCount = log.AttemptCount + 1
                    });
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return outcomes;
    }

    private async Task<DeliveryOutcome> DeliverSingleLogAsync(
        FcmNotificationLog log,
        Dictionary<int, UserDevice> devices,
        Dictionary<int, Alert> alerts,
        CancellationToken cancellationToken)
    {
        var outcome = new DeliveryOutcome
        {
            LogId = log.Id,
            NewAttemptCount = log.AttemptCount + 1
        };

        // 1. Check expiration date
        if (log.ExpiresAt.HasValue && log.ExpiresAt.Value < DateTime.UtcNow)
        {
            outcome.Classification = FcmNotificationRetryConstants.Outcome.Stale;
            outcome.ErrorMessage = "Notification has expired.";
            return outcome;
        }

        // 2. Check alert reference validity (If Alert is already RESOLVED, it is stale)
        if (log.ReferenceType == NotificationConstants.ReferenceType.Alert && log.ReferenceId.HasValue)
        {
            if (alerts.TryGetValue(log.ReferenceId.Value, out var alert))
            {
                if (alert.Status == AlertConstants.Status.Resolved)
                {
                    outcome.Classification = FcmNotificationRetryConstants.Outcome.Stale;
                    outcome.ErrorMessage = "Referenced Alert is already RESOLVED.";
                    return outcome;
                }
            }
        }

        // 3. Resolve device & token
        if (!log.UserDeviceId.HasValue || !devices.TryGetValue(log.UserDeviceId.Value, out var device))
        {
            outcome.Classification = FcmNotificationRetryConstants.Outcome.PayloadInvalid;
            outcome.ErrorMessage = "UserDevice target missing or already deactivated.";
            return outcome;
        }

        if (string.IsNullOrWhiteSpace(device.DeviceToken))
        {
            outcome.Classification = FcmNotificationRetryConstants.Outcome.PayloadInvalid;
            outcome.ErrorMessage = "DeviceToken is empty.";
            return outcome;
        }

        // Validate that token belongs to the UserId logged
        if (device.UserId != log.UserId)
        {
            outcome.Classification = FcmNotificationRetryConstants.Outcome.PayloadInvalid;
            outcome.ErrorMessage = "Device token owner mismatch (Organization/User boundary violation).";
            return outcome;
        }

        // 4. Send Message via Firebase Messaging
        try
        {
            var dataDict = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(log.DataPayload))
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(log.DataPayload);
                    if (parsed != null)
                    {
                        foreach (var kvp in parsed)
                        {
                            dataDict[kvp.Key] = kvp.Value ?? "";
                        }
                    }
                }
                catch
                {
                    outcome.Classification = FcmNotificationRetryConstants.Outcome.PayloadInvalid;
                    outcome.ErrorMessage = "Malformed DataPayload JSON.";
                    return outcome;
                }
            }

            // Call external SDK (outside transaction)
            var response = await _fcmClient.SendAsync(device.DeviceToken, log.Title, log.Body, dataDict, cancellationToken);

            outcome.Classification = FcmNotificationRetryConstants.Outcome.Success;
            outcome.ProviderMessageId = response;
        }
        catch (Exception ex)
        {
            var classification = _classifier.Classify(ex);
            outcome.Classification = classification;
            outcome.ErrorMessage = ex.Message;
            
            outcome.ErrorCode = _classifier.GetErrorCode(ex) ?? "Exception";

            // Extract Retry-After if available (best-effort)
            var msgLower = ex.Message.ToLowerInvariant();
            if (msgLower.Contains("retry-after") || msgLower.Contains("retryafter"))
            {
                // Try parsing seconds from the exception message
                var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"\d+");
                if (match.Success && int.TryParse(match.Value, out var parsedSeconds))
                {
                    outcome.RetryAfterSeconds = parsedSeconds;
                }
            }
        }

        return outcome;
    }

    private async Task FinalizeBatchAsync(
        List<DeliveryOutcome> outcomes,
        string runId,
        FcmNotificationRetryResult result,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        using var transaction = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var logIds = outcomes.Select(o => o.LogId).ToList();
            var logs = await _context.FcmNotificationLogs
                .Where(x => logIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var deviceDeactivations = new List<int>();

            foreach (var outcome in outcomes)
            {
                var log = logs.FirstOrDefault(x => x.Id == outcome.LogId);
                if (log == null || log.ProcessingBy != runId)
                {
                    _logger.LogWarning("[JOB-05] Concurrency conflict: log has been modified or claimed by another worker. skipping. notificationLogId={LogId}", outcome.LogId);
                    result.Skipped++;
                    continue;
                }

                log.AttemptCount = outcome.NewAttemptCount;
                log.LastAttemptAt = now;
                log.LastModifiedDate = now;

                // Clear lease fields
                log.ProcessingStartedAt = null;
                log.ProcessingLeaseUntil = null;
                log.ProcessingBy = null;

                if (outcome.Classification == FcmNotificationRetryConstants.Outcome.Success)
                {
                    log.Status = FcmNotificationRetryConstants.Status.Sent;
                    log.IsSent = true;
                    log.SentAt = now;
                    log.ProviderMessageId = outcome.ProviderMessageId;
                    log.ErrorMessage = null;
                    log.LastErrorCode = null;
                    log.NextRetryAt = null;

                    result.Sent++;
                    result.Processed++;
                }
                else if (outcome.Classification == FcmNotificationRetryConstants.Outcome.Stale)
                {
                    log.Status = FcmNotificationRetryConstants.Status.Stale;
                    log.ErrorMessage = outcome.ErrorMessage;
                    log.LastErrorCode = "STALE";

                    result.Stale++;
                    result.Processed++;
                }
                else if (outcome.Classification == FcmNotificationRetryConstants.Outcome.PayloadInvalid)
                {
                    log.Status = FcmNotificationRetryConstants.Status.FailedPermanent;
                    log.ErrorMessage = outcome.ErrorMessage;
                    log.LastErrorCode = "PAYLOAD_INVALID";

                    result.PayloadInvalid++;
                    result.PermanentFailed++;
                    result.Processed++;
                }
                else if (outcome.Classification == FcmNotificationRetryConstants.Outcome.InvalidToken)
                {
                    log.Status = FcmNotificationRetryConstants.Status.FailedPermanent;
                    log.IsDeleted = true; // Soft-delete invalid token per message log according to FDS baseline
                    log.ErrorMessage = outcome.ErrorMessage;
                    log.LastErrorCode = outcome.ErrorCode ?? "INVALID_TOKEN";

                    if (log.UserDeviceId.HasValue)
                    {
                        deviceDeactivations.Add(log.UserDeviceId.Value);
                    }

                    result.InvalidTokens++;
                    result.PermanentFailed++;
                    result.Processed++;
                }
                else if (outcome.Classification == FcmNotificationRetryConstants.Outcome.GlobalConfiguration)
                {
                    // Global config error: do NOT increment AttemptCount and do NOT exhaust attempts. Keep status PENDING.
                    log.Status = FcmNotificationRetryConstants.Status.RetryScheduled;
                    log.AttemptCount = log.AttemptCount - 1; // Rollback increment
                    log.NextRetryAt = now.AddMinutes(5); // Delay next run to allow administrator configuration fix
                    log.LastErrorCode = outcome.ErrorCode ?? "GLOBAL_CONFIG";
                    log.ErrorMessage = "FCM global credential or server authentication error. Execution paused.";

                    _logger.LogCritical("[JOB-05] Global credentials or configuration error detected: {Error}. Automatic retry paused for notificationLogId={LogId}", outcome.ErrorMessage, log.Id);

                    result.Processed++;
                }
                else
                {
                    // Transient or other failure
                    log.LastErrorCode = outcome.ErrorCode ?? "TRANSIENT_ERROR";
                    log.ErrorMessage = outcome.ErrorMessage != null && outcome.ErrorMessage.Length > 500
                        ? outcome.ErrorMessage[..500]
                        : outcome.ErrorMessage;

                    if (log.AttemptCount >= FcmNotificationRetryConstants.DefaultOptions.MaxAttempts)
                    {
                        log.Status = FcmNotificationRetryConstants.Status.Exhausted;
                        log.NextRetryAt = null;

                        result.Exhausted++;
                        result.PermanentFailed++;
                    }
                    else
                    {
                        log.Status = FcmNotificationRetryConstants.Status.RetryScheduled;

                        // Calculate exponential backoff
                        var seconds = Math.Min(
                            FcmNotificationRetryConstants.DefaultOptions.MaxBackoffSeconds,
                            FcmNotificationRetryConstants.DefaultOptions.InitialBackoffSeconds * Math.Pow(2, log.AttemptCount - 1));

                        // If Retry-After says longer, prioritize it
                        if (outcome.RetryAfterSeconds.HasValue && outcome.RetryAfterSeconds.Value > seconds)
                        {
                            seconds = outcome.RetryAfterSeconds.Value;
                        }

                        log.NextRetryAt = now.AddSeconds(seconds);
                        result.RetryScheduled++;
                    }

                    result.Processed++;
                }

                _logger.LogInformation(
                    "[JOB-05] Log processed. notificationLogId={LogId}, maskedUserDeviceId={MaskedUserDevice}, notificationType={Type}, attempt={Attempt}, action={Action}, lastErrorCode={Code}, newStatus={Status}",
                    log.Id,
                    log.UserDeviceId.HasValue ? $"***{log.UserDeviceId.Value % 100}" : "null",
                    log.TriggerType ?? "UNKNOWN",
                    log.AttemptCount,
                    outcome.Classification == FcmNotificationRetryConstants.Outcome.Success ? "SENT" : "FAILED",
                    log.LastErrorCode,
                    log.Status);
            }

            // Deactivate invalid devices by setting IsDeleted = true
            if (deviceDeactivations.Count > 0)
            {
                var distinctDeviceIds = deviceDeactivations.Distinct().ToList();
                var dbDevices = await _context.UserDevices
                    .Where(x => distinctDeviceIds.Contains(x.Id) && !x.IsDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var device in dbDevices)
                {
                    device.IsDeleted = true;
                    device.LastModifiedDate = now;
                    _logger.LogWarning("[JOB-05] Invalid FCM token detected. Deactivating UserDevice registry. userDeviceId={DeviceId}, userId={UserId}", device.Id, device.UserId);
                    result.DevicesRevoked++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-05] Fatal error finalising delivery outcomes database commit. runId={RunId}", runId);
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    private class DeliveryOutcome
    {
        public int LogId { get; set; }
        public string Classification { get; set; } = null!;
        public string? ProviderMessageId { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int NewAttemptCount { get; set; }
        public int? RetryAfterSeconds { get; set; }
    }
}
