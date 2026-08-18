using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.IntakeBottleneck;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class IntakeBottleneckEvaluationJob : IScheduledJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IntakeBottleneckEvaluationJob> _logger;

    public IntakeBottleneckEvaluationJob(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<IntakeBottleneckEvaluationJob>();
    }

    public async Task ExecuteAsync()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("{LogPrefix} Intake Bottleneck Evaluation started. RunId: {RunId}, StartedAt: {Time}", IntakeBottleneckConstants.Job.LogPrefix, runId, DateTime.UtcNow);

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var evaluationService = scope.ServiceProvider.GetRequiredService<IIntakeBottleneckEvaluationService>();

            var result = await evaluationService.EvaluateAllAsync(CancellationToken.None);

            _logger.LogInformation("{LogPrefix} completed. RunId: {RunId}, processed={Processed}, skipped={Skipped}, failed={Failed}, alertsCreated={AlertsCreated}, alertsUpdated={AlertsUpdated}, alertsResolved={AlertsResolved}, notificationsQueued={NotificationsQueued}, durationMs={DurationMs}, skippedByLock={SkippedByLock}",
                IntakeBottleneckConstants.Job.LogPrefix,
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
            _logger.LogError(ex, "{LogPrefix} failed. RunId: {RunId}", IntakeBottleneckConstants.Job.LogPrefix, runId);
        }
    }
}
