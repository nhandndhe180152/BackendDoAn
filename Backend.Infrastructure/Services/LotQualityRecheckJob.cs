using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.LotQualityRecheck;
using Backend.Share.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class LotQualityRecheckJob : IScheduledJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LotQualityRecheckJob> _logger;

    public LotQualityRecheckJob(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<LotQualityRecheckJob>();
    }

    public async Task ExecuteAsync()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("{LogPrefix} Lot Quality Recheck Evaluation started. RunId: {RunId}, StartedAt: {Time}",
            LotQualityRecheckConstants.Job.LogPrefix, runId, DateTime.UtcNow);

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var lotQualityService = scope.ServiceProvider.GetRequiredService<ILotQualityRecheckService>();

            var result = await lotQualityService.EvaluateAllLotsAsync(CancellationToken.None);

            _logger.LogInformation("{LogPrefix} completed. RunId: {RunId}, processed={Processed}, skipped={Skipped}, failed={Failed}, alertsCreated={AlertsCreated}, alertsUpdated={AlertsUpdated}, alertsResolved={AlertsResolved}, notificationsQueued={NotificationsQueued}, durationMs={DurationMs}, skippedByLock={SkippedByLock}",
                LotQualityRecheckConstants.Job.LogPrefix,
                runId,
                result.Processed,
                result.Skipped,
                result.Failed,
                result.AlertsCreated,
                result.AlertsUpdated,
                result.AlertsResolved,
                result.NotificationsQueued,
                result.DurationMs,
                result.SkippedByLock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LogPrefix} failed. RunId: {RunId}", LotQualityRecheckConstants.Job.LogPrefix, runId);
        }
    }
}
