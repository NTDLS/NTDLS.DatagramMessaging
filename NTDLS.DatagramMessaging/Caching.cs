using Microsoft.Extensions.Caching.Memory;
using System;

namespace NTDLS.DatagramMessaging
{
    internal static class Caching
    {
        internal static IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        internal static MemoryCacheEntryOptions _slidingOneMinute = new() { SlidingExpiration = TimeSpan.FromMinutes(1) };
        internal static MemoryCacheEntryOptions _slidingThirtySeconds = new() { SlidingExpiration = TimeSpan.FromSeconds(30) };

        public static bool CacheTryGet<T>(object key, out T? value)
            => _cache.TryGetValue(key, out value);

        public static void CacheSet(object key, object value, TimeSpan slidingExpiration)
            => _cache.Set(key, value, new MemoryCacheEntryOptions() { SlidingExpiration = slidingExpiration });

        public static void CacheSetOneMinute(object key, object value)
            => _cache.Set(key, value, _slidingOneMinute);

        public static void CacheSetThirtySeconds(object key, object value)
            => _cache.Set(key, value, _slidingThirtySeconds);
    }
}
