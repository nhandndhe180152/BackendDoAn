using System;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class UserSessionCleanupJob : IScheduledJob
{
    private readonly BackendContext _db;
    private readonly ILogger<UserSessionCleanupJob> _logger;

    public UserSessionCleanupJob(BackendContext db, ILoggerFactory loggerFactory)
    {
        _db = db;
        _logger = loggerFactory.CreateLogger<UserSessionCleanupJob>();
    }

    public async Task ExecuteAsync()
    {
        var currentDate = DateTime.Now;
        _logger.LogInformation("[UserSessionCleanupJob] started at {Time}", currentDate);

        var expiredSessions = await _db.UserSessions
            .Where(x => x.ExpirationDate < currentDate || x.IsUsed)
            .ToListAsync();

        if (expiredSessions.Count == 0)
        {
            _logger.LogInformation("[UserSessionCleanupJob] No expired session.");
            return;
        }

        _db.UserSessions.RemoveRange(expiredSessions);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[UserSessionCleanupJob] Cleaned up {Count} expired user sessions.", expiredSessions.Count);
    }
}
