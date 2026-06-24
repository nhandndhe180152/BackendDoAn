using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

/// <summary>
/// JOB-03 - IoT Device Heartbeat Check.
/// Đánh dấu thiết bị Online/Offline dựa trên LastHeartbeat và ngưỡng timeout cấu hình (BR-47).
/// Chỉ cập nhật khi trạng thái tính toán khác trạng thái đang lưu (idempotent).
/// </summary>
public class IotDeviceHeartbeatJob : IScheduledJob
{
    // Khóa cấu hình ngưỡng timeout (giây). Có thể override trong SystemConfig.
    private const string TimeoutConfigKey = "IotHeartbeatTimeoutSeconds";
    private const int DefaultTimeoutSeconds = 120;

    private readonly BackendContext _db;
    private readonly ILogger<IotDeviceHeartbeatJob> _logger;

    public IotDeviceHeartbeatJob(BackendContext db, ILoggerFactory loggerFactory)
    {
        _db = db;
        _logger = loggerFactory.CreateLogger<IotDeviceHeartbeatJob>();
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.Now;
        _logger.LogInformation("[IotDeviceHeartbeatJob] started at {Time}", now);

        var timeoutSeconds = await ResolveTimeoutSecondsAsync();
        var threshold = now.AddSeconds(-timeoutSeconds);

        var devices = await _db.IotDevices
            .Where(x => x.IsActive && !x.IsDeleted)
            .ToListAsync();

        var changed = 0;
        foreach (var device in devices)
        {
            var shouldBeOnline = device.LastHeartbeat.HasValue && device.LastHeartbeat.Value >= threshold;

            if (device.IsOnline != shouldBeOnline)
            {
                device.IsOnline = shouldBeOnline;
                device.LastModifiedDate = now;
                changed++;
            }
        }

        if (changed == 0)
        {
            _logger.LogInformation("[IotDeviceHeartbeatJob] No device state change.");
            return;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[IotDeviceHeartbeatJob] Updated online/offline state for {Count} device(s).", changed);
    }

    private async Task<int> ResolveTimeoutSecondsAsync()
    {
        var raw = await _db.SystemConfigs
            .Where(x => x.ConfigKey == TimeoutConfigKey)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var seconds) && seconds > 0)
        {
            return seconds;
        }

        return DefaultTimeoutSeconds;
    }
}
