using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

/// <summary>
/// JOB-04 - IoT Command Expiry.
/// Chuyển các lệnh IoT đang PENDING đã quá ExpiredAt sang EXPIRED (BR-48).
/// Các trạng thái kết thúc (EXECUTED/FAILED/EXPIRED/CANCELLED) không bị thay đổi lại.
/// Giá trị status khớp với Backend.Application.Constants.IotDeviceCommandConstants.
/// </summary>
public class IotCommandExpiryJob : IScheduledJob
{
    private const string StatusPending = "PENDING";
    private const string StatusExpired = "EXPIRED";

    private readonly BackendContext _db;
    private readonly ILogger<IotCommandExpiryJob> _logger;

    public IotCommandExpiryJob(BackendContext db, ILoggerFactory loggerFactory)
    {
        _db = db;
        _logger = loggerFactory.CreateLogger<IotCommandExpiryJob>();
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.Now;
        _logger.LogInformation("[IotCommandExpiryJob] started at {Time}", now);

        var expiredCommands = await _db.IotDeviceCommands
            .Where(x =>
                !x.IsDeleted &&
                x.Status == StatusPending &&
                x.ExpiredAt != null &&
                x.ExpiredAt <= now)
            .ToListAsync();

        if (expiredCommands.Count == 0)
        {
            _logger.LogInformation("[IotCommandExpiryJob] No expired command.");
            return;
        }

        foreach (var command in expiredCommands)
        {
            command.Status = StatusExpired;
            command.ResultMessage = "Command expired before device picked it up.";
            command.LastModifiedDate = now;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("[IotCommandExpiryJob] Expired {Count} pending command(s).", expiredCommands.Count);
    }
}
