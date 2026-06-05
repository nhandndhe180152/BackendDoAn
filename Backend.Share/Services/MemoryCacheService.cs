using System;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Share.Services;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    public Task<T?> GetAsync<T>(string key)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null, CacheItemPriority? priority = null)
    {
        var options = new MemoryCacheEntryOptions();
        if (absoluteExpiration.HasValue)
            options.SetAbsoluteExpiration(absoluteExpiration.Value);
        if (slidingExpiration.HasValue)
            options.SetSlidingExpiration(slidingExpiration.Value);
        if (priority.HasValue)
            options.SetPriority(priority.Value);

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }
}
