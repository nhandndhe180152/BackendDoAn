using System;
using Backend.Share.Helpers;

namespace Backend.Infrastructure.DependencyInjection.Options;

public class ScheduledJobConfig
{
    public JobSetting CleanupUserSession { get; set; } = new();
    public JobSetting CleanupVerificationTokens { get; set; } = new();
    public JobSetting CreateDriverSalaries { get; set; } = new();
    public JobSetting PingDatabase { get; set; } = new();

    // JOB-03: kiểm tra heartbeat thiết bị IoT, đánh dấu Offline khi quá hạn (BR-47).
    // Tài liệu yêu cầu 60s; Hangfire cron tối thiểu 1 phút nên dùng EveryMinute(1).
    public JobSetting IotDeviceHeartbeat { get; set; } = new()
    {
        Enabled = true,
        Cron = CronHelper.EveryMinute(1)
    };

    // JOB-04: hết hạn lệnh IoT đang Pending (BR-48). Tài liệu yêu cầu 30s; dùng EveryMinute(1).
    public JobSetting IotCommandExpiry { get; set; } = new()
    {
        Enabled = true,
        Cron = CronHelper.EveryMinute(1)
    };
}

public class JobSetting
{
    public bool Enabled { get; set; } = false;
    public string Cron { get; set; } = CronHelper.Hourly; // default: mỗi giờ
}
