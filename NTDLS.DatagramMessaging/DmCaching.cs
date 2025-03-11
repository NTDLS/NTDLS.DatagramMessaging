using Microsoft.Extensions.Caching.Memory;
using System;

namespace NTDLS.DatagramMessaging
{
    internal static class DmCaching
    {
        internal static IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        internal static MemoryCacheEntryOptions _slidingOneMinute = new() { SlidingExpiration = TimeSpan.FromMinutes(1) };
        internal static MemoryCacheEntryOptions _slidingThirtySeconds = new() { SlidingExpiration = TimeSpan.FromSeconds(30) };
        internal static MemoryCacheEntryOptions _slidingTenMinutes = new() { SlidingExpiration = TimeSpan.FromMinutes(10) };

        public static bool TryGet<T>(object key, out T? value)
            => _cache.TryGetValue(key, out value);

        public static void Set(object key, object value, TimeSpan slidingExpiration)
            => _cache.Set(key, value, new MemoryCacheEntryOptions() { SlidingExpiration = slidingExpiration });

        public static void SetOneMinute(object key, object value)
            => _cache.Set(key, value, _slidingOneMinute);

        public static void SetTenMinutes(object key, object value)
            => _cache.Set(key, value, _slidingTenMinutes);

        public static void SetThirtySeconds(object key, object value)
            => _cache.Set(key, value, _slidingThirtySeconds);
    }
}
