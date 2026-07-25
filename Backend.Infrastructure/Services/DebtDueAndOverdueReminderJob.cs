using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Backend.Share.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class DebtDueAndOverdueReminderJob : IScheduledJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DebtDueAndOverdueReminderJob> _logger;

    public DebtDueAndOverdueReminderJob(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<DebtDueAndOverdueReminderJob>();
    }

    public async Task ExecuteAsync()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("{LogPrefix} Debt Due & Overdue Reminder Evaluation started. RunId: {RunId}, StartedAt: {Time}",
            DebtDueOverdueConstants.Job.LogPrefix, runId, DateTime.UtcNow);

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var reminderService = scope.ServiceProvider.GetRequiredService<IDebtDueAndOverdueReminderService>();

            var result = await reminderService.EvaluateAllDebtsAsync(CancellationToken.None);

            _logger.LogInformation("{LogPrefix} completed. RunId: {RunId}, processed={Processed}, skipped={Skipped}, failed={Failed}, payableProcessed={PayableProcessed}, receivableProcessed={ReceivableProcessed}, dueSoonCreated={DueSoonCreated}, overdueCreated={OverdueCreated}, alertsUpdated={AlertsUpdated}, alertsEscalated={AlertsEscalated}, alertsResolved={AlertsResolved}, notificationsQueued={NotificationsQueued}, durationMs={DurationMs}, skippedByLock={SkippedByLock}, completedAt={CompletedAt}",
                DebtDueOverdueConstants.Job.LogPrefix,
                runId,
                result.Processed,
                result.Skipped,
                result.Failed,
                result.PayableProcessed,
                result.ReceivableProcessed,
                result.DueSoonCreated,
                result.OverdueCreated,
                result.AlertsUpdated,
                result.AlertsEscalated,
                result.AlertsResolved,
                result.NotificationsQueued,
                result.DurationMs,
                result.SkippedByLock,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LogPrefix} failed. RunId: {RunId}", DebtDueOverdueConstants.Job.LogPrefix, runId);
        }
    }
}
