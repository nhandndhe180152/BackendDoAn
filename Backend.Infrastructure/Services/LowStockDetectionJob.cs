using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.LowStock;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class LowStockDetectionJob : IScheduledJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LowStockDetectionJob> _logger;

    public LowStockDetectionJob(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<LowStockDetectionJob>();
    }

    public async Task ExecuteAsync()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[JOB-01] Low Stock Detection started. RunId: {RunId}, StartedAt: {Time}", runId, DateTime.UtcNow);

        try
        {
            // Resolve service trong scope mới để đảm bảo DbContext độc lập và sạch sẽ
            await using var scope = _serviceProvider.CreateAsyncScope();
            var detectionService = scope.ServiceProvider.GetRequiredService<ILowStockDetectionService>();

            var result = await detectionService.DetectLowStockAsync(CancellationToken.None);

            _logger.LogInformation("[JOB-01] completed. RunId: {RunId}, processed={Processed}, created={Created}, updated={Updated}, resolved={Resolved}, skipped={Skipped}, failed={Failed}, durationMs={DurationMs}",
                runId,
                result.Processed,
                result.Created,
                result.Updated,
                result.Resolved,
                result.Skipped,
                result.Failed,
                result.DurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JOB-01] failed. RunId: {RunId}", runId);
        }
    }
}
