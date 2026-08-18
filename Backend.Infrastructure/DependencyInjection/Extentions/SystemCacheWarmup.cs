using System;
using System.Linq;
using Backend.Application.Common;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
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

        // Backfill Menu.Code từ enum Enums.Menu (bảng Menu KHÔNG được seed trong code).
        // Dùng CHÍNH ((Menu)id).ToString() giống CustomAuthorize để Code luôn khớp — kể cả khi enum có
        // giá trị trùng (vd SYSTEM_CONFIG/TAG_TYPE = 26): cả hai cùng ra 1 chuỗi nên vẫn nhất quán.
        try
        {
            var codeById = Enum.GetValues<Enums.Menu>()
                .Cast<int>()
                .Distinct()
                .ToDictionary(id => id, id => ((Enums.Menu)id).ToString());

            var menus = await dbContext.Set<Menu>().Where(m => !m.IsDeleted).ToListAsync();
            var changed = false;
            foreach (var m in menus)
            {
                if (codeById.TryGetValue(m.Id, out var code) && m.Code != code)
                {
                    m.Code = code;
                    changed = true;
                }
            }
            if (changed)
            {
                await dbContext.SaveChangesAsync();
                logger?.LogInformation("Backfilled Menu.Code from Enums.Menu.");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Menu.Code backfill failed.");
        }

        // Nạp bảng lookup (Menu/Action/Role/UserStatus/StockTakeStatus) Code→Id và gắn facade tĩnh.
        try
        {
            var systemLookup = scope.ServiceProvider.GetRequiredService<ISystemLookup>();
            await systemLookup.ReloadAsync();
            Lookup.Configure(systemLookup);
            logger?.LogInformation("Preloaded SystemLookup (Code->Id) to cache successfully.");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to preload SystemLookup.");
        }

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
