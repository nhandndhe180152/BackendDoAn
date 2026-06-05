using System;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class VerificationTokenCleanupJob : IScheduledJob
{
    private readonly BackendContext _db;
    private readonly ILogger<VerificationTokenCleanupJob> _logger;

    public VerificationTokenCleanupJob(BackendContext db, ILoggerFactory loggerFactory)
    {
        _db = db;
        _logger = loggerFactory.CreateLogger<VerificationTokenCleanupJob>();
    }

    public async Task ExecuteAsync()
    {
        var currentDate = DateTime.Now;
        _logger.LogInformation("[VerificationTokenCleanupJob] started at {Time}", currentDate);

        var expiredTokens = await _db.UserVerificationTokens
            .Where(x => x.ExpirationDate < currentDate || x.IsUsed)
            .ToListAsync();

        if (expiredTokens.Count == 0)
        {
            _logger.LogInformation("[VerificationTokenCleanupJob] No expired token.");
            return;
        }

        _db.UserVerificationTokens.RemoveRange(expiredTokens);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[VerificationTokenCleanupJob] Cleaned up {Count} expired verification tokens.", expiredTokens.Count);
    }
}
