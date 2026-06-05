using System;
using Backend.Application.Constants;
using Backend.Infrastructure.Persistence;
using Backend.Share.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.DependencyInjection.Extentions;

public static class SystemCacheWarmup
{
    public static async Task LoadCacheAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BackendContext>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("CacheWarmup");

        try
        {
            var permissions = await dbContext.Permissions
                .Where(x => !x.IsDeleted)
                .ToListAsync();
            await cacheService.SetAsync(CommonConstants.Cache.PERMISSIONS_ALL_KEY, permissions);

            logger?.LogInformation("Preloaded Permissions:All to cache successfully.");

            var systemConfigs = await dbContext.SystemConfigs
                .Where(x => !x.IsDeleted)
                .ToListAsync();
            await cacheService.SetAsync(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY, systemConfigs);

            logger?.LogInformation("Preloaded SystemConfig:All to cache successfully.");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to preload Permissions to cache.");
        }

        try
        {

            var systemConfigs = await dbContext.SystemConfigs
                .Where(x => !x.IsDeleted)
                .ToListAsync();
            await cacheService.SetAsync(CommonConstants.Cache.SYSTEMCONFIG_ALL_KEY, systemConfigs);

            logger?.LogInformation("Preloaded SystemConfig:All to cache successfully.");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to preload SystemConfig to cache.");
        }
    }

}
