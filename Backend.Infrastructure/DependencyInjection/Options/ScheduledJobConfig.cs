using System;
using Backend.Share.Helpers;

namespace Backend.Infrastructure.DependencyInjection.Options;

public class ScheduledJobConfig
{
    public JobSetting CleanupUserSession { get; set; } = new();
    public JobSetting CleanupVerificationTokens { get; set; } = new();
    public JobSetting CreateDriverSalaries { get; set; } = new();
    public JobSetting PingDatabase { get; set; } = new();
}

public class JobSetting
{
    public bool Enabled { get; set; } = false;
    public string Cron { get; set; } = CronHelper.Hourly; // default: mỗi giờ
}
