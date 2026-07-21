using System;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.LowStock;
using Backend.Share.Services;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class LowStockDetectionJob : IScheduledJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICacheService _cacheService;
    private readonly ILogger<LowStockDetectionJob> _logger;

    public LowStockDetectionJob(
        IServiceProvider serviceProvider, 
        ICacheService cacheService,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _cacheService = cacheService;
        _logger = loggerFactory.CreateLogger<LowStockDetectionJob>();
    }

    public async Task ExecuteAsync()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var lockKey = "stocklite:job-01";

        // 1. Check distributed lock
        if (await _cacheService.ExistsAsync(lockKey))
        {
            _logger.LogWarning("[JOB-01] Skip execution. Reason: lock_exists (Job is already running). RunId: {RunId}", runId);
            return;
        }

        // 2. Acquire lock (TTL = 5 minutes)
        await _cacheService.SetAsync(lockKey, "running", TimeSpan.FromMinutes(5));
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
        finally
        {
            // 3. Release lock
            await _cacheService.RemoveAsync(lockKey);
        }
    }
}
