using Microsoft.Extensions.Caching.Memory;
using System;

namespace NTDLS.DatagramMessaging
{
    internal static class DmCaching
    {
        internal static IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        internal static MemoryCacheEntryOptions _slidingOneMinute = new() { SlidingExpiration = TimeSpan.FromMinutes(1) };
        internal static MemoryCacheEntryOptions _slidingTenMinutes = new() { SlidingExpiration = TimeSpan.FromMinutes(10) };

        public static TItem GetOrCreate<TItem>(object key, MemoryCacheEntryOptions options, Func<ICacheEntry, TItem> factory)
            => _cache.GetOrCreate<TItem>(key, factory, options) ?? throw new ArgumentNullException(nameof(key));

        public static TItem GetOrCreateOneMinute<TItem>(object key, Func<ICacheEntry, TItem> factory)
            => GetOrCreate<TItem>(key, _slidingOneMinute, factory);

        public static TItem GetOrCreateTenMinutes<TItem>(object key, Func<ICacheEntry, TItem> factory)
            => GetOrCreate<TItem>(key, _slidingTenMinutes, factory);
    }
}
