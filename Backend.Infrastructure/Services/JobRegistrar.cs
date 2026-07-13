using System;
using Backend.Infrastructure.DependencyInjection.Options;
using Backend.Share.Services;
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
}
