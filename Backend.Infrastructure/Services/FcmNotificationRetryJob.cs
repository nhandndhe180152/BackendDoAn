using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.FcmNotificationRetry;
using Backend.Share.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class FcmNotificationRetryJob : IScheduledJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FcmNotificationRetryJob> _logger;

    public FcmNotificationRetryJob(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<FcmNotificationRetryJob>();
    }

    public async Task ExecuteAsync()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("{LogPrefix} FCM Notification Retry Job started. RunId: {RunId}, StartedAt: {Time}",
            FcmNotificationRetryConstants.Job.LogPrefix, runId, DateTime.UtcNow);

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var retryService = scope.ServiceProvider.GetRequiredService<IFcmNotificationRetryService>();

            var result = await retryService.RunRetryJobAsync(CancellationToken.None);

            if (result.SkippedByLock)
            {
                _logger.LogInformation("{LogPrefix} execution skipped because another instance is already running.",
                    FcmNotificationRetryConstants.Job.LogPrefix);
                return;
            }

            _logger.LogInformation("{LogPrefix} completed. RunId: {RunId}, eligible={Eligible}, claimed={Claimed}, processed={Processed}, sent={Sent}, retryScheduled={RetryScheduled}, permanentFailed={PermanentFailed}, invalidTokens={InvalidTokens}, devicesRevoked={DevicesRevoked}, exhausted={Exhausted}, stale={Stale}, payloadInvalid={PayloadInvalid}, leaseRecovered={LeaseRecovered}, skipped={Skipped}, failed={Failed}, durationMs={DurationMs}, completedAt={CompletedAt}",
                FcmNotificationRetryConstants.Job.LogPrefix,
                runId,
                result.Eligible,
                result.Claimed,
                result.Processed,
                result.Sent,
                result.RetryScheduled,
                result.PermanentFailed,
                result.InvalidTokens,
                result.DevicesRevoked,
                result.Exhausted,
                result.Stale,
                result.PayloadInvalid,
                result.LeaseRecovered,
                result.Skipped,
                result.Failed,
                result.DurationMs,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LogPrefix} failed. RunId: {RunId}", FcmNotificationRetryConstants.Job.LogPrefix, runId);
        }
    }
}
