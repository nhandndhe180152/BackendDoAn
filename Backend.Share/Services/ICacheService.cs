using System;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Share.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null, CacheItemPriority? priority = null);
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
}
