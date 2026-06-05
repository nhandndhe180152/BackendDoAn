using System;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class PingDatabaseJob : IScheduledJob
{
    private readonly BackendContext _db;
    private readonly ILogger<PingDatabaseJob> _logger;

    public PingDatabaseJob(BackendContext db, ILoggerFactory loggerFactory)
    {
        _db = db;
        _logger = loggerFactory.CreateLogger<PingDatabaseJob>();
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("[PingDatabaseJob] Pinging database to keep alive at {Time}", DateTime.Now);
        
        try 
        {
            // Thực thi lệnh SELECT 1 để duy trì kết nối
            await _db.Database.ExecuteSqlRawAsync("SELECT 1");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PingDatabaseJob] Failed to ping database.");
        }
    }
}