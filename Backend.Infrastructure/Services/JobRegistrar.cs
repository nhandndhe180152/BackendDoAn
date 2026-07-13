using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Infrastructure.DependencyInjection.Options;
using Backend.Share.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Services;

public class JobRegistrar : IJobRegistrar
{
    private readonly IScheduledJobService _scheduler;
    private readonly ScheduledJobConfig _config;

    public JobRegistrar(
        IOptions<ScheduledJobConfig> config,
        IScheduledJobService scheduler)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    /// <summary>
    /// Recurring job IoT đã gỡ theo BLE pivot (FDS v3). Định nghĩa cũ vẫn còn nằm trong
    /// Hangfire storage nên scheduler cố nạp type đã xóa → JobLoadException. Xóa idempotent
    /// mỗi lần khởi động để dọn sạch (RemoveIfExists an toàn khi không còn tồn tại).
    /// </summary>
    private static readonly string[] RetiredJobIds =
    {
        "IotDeviceHeartbeatJob",
        "IotCommandExpiryJob"
    };

    public void RegisterJobs()
    {
        foreach (var retiredJobId in RetiredJobIds)
        {
            _scheduler.DeleteRecurringJob(retiredJobId);
        }

        // Dọn các JOB INSTANCE mồ côi đã enqueue/scheduled/retry trước khi gỡ code IoT.
        PurgeBrokenJobInstances();

        RegisterJob<UserSessionCleanupJob>(
            nameof(UserSessionCleanupJob),
            _config.CleanupUserSession.Enabled,
            _config.CleanupUserSession.Cron
        );

        RegisterJob<VerificationTokenCleanupJob>(
            nameof(VerificationTokenCleanupJob),
            _config.CleanupVerificationTokens.Enabled,
            _config.CleanupVerificationTokens.Cron
        );

        // RegisterJob<PingDatabaseJob>(
        // nameof(PingDatabaseJob),
        // _config.PingDatabase.Enabled,
        // _config.PingDatabase.Cron
        // );
    }

    private void RegisterJob<TJob>(string name, bool enabled, string cron)
        where TJob : IScheduledJob
    {
        if (!enabled) return;

        _scheduler.AddOrUpdate<TJob>(name, job => job.ExecuteAsync(), cron);
    }

    /// <summary>
    /// Xóa các job INSTANCE (đã enqueue/scheduled/processing/retry/failed) mà Hangfire KHÔNG nạp được
    /// type (Job == null) — điển hình là job của type đã xóa như IotCommandExpiryJob/IotDeviceHeartbeatJob
    /// đã bị enqueue TRƯỚC khi gỡ code, gây TypeLoadException và retry mãi. RemoveIfExists chỉ xóa định
    /// nghĩa recurring, không xóa các instance này. Best-effort: bọc try/catch, không làm vỡ startup.
    /// </summary>
    private static void PurgeBrokenJobInstances()
    {
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var brokenIds = new List<string>();

            brokenIds.AddRange(monitor.ScheduledJobs(0, 1000).Where(x => x.Value.Job == null).Select(x => x.Key));
            brokenIds.AddRange(monitor.ProcessingJobs(0, 1000).Where(x => x.Value.Job == null).Select(x => x.Key));
            brokenIds.AddRange(monitor.FailedJobs(0, 1000).Where(x => x.Value.Job == null).Select(x => x.Key));

            foreach (var queue in monitor.Queues())
            {
                brokenIds.AddRange(monitor.EnqueuedJobs(queue.Name, 0, 1000)
                    .Where(x => x.Value.Job == null).Select(x => x.Key));
            }

            foreach (var id in brokenIds.Distinct())
            {
                BackgroundJob.Delete(id);
            }
        }
        catch
        {
            // Dọn dẹp best-effort — bỏ qua lỗi để không ảnh hưởng khởi động.
        }
    }
}
