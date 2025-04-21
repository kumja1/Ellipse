using Microsoft.Extensions.Caching.Memory;

namespace Ellipse.Server.Utils;

internal static class SingletonMemoryCache
{
    private static readonly MemoryCache _inner = new(
        new MemoryCacheOptions()
        {
            CompactionPercentage = 0.2,
            ExpirationScanFrequency = TimeSpan.FromMinutes(10),
            TrackStatistics = false,
            TrackLinkedCacheEntries = false,
        }
    );

    public static void SetEntry<T, TItem>(
        object key,
        TItem value,
        MemoryCacheEntryOptions? options = null
    ) => _inner.Set(CompositeKey<T>(key), value, options);

    public static bool TryGetEntry<T, TItem>(object key, out TItem? item) =>
        _inner.TryGetValue(CompositeKey<T>(key), out item);

    public static TItem GetOrCreateEntry<T, TItem>(object key, TItem value)
    {
        string keyStr = CompositeKey<T>(key);
        return _inner.GetOrCreate(keyStr, entry => entry?.Value is TItem item ? item : value)!;
    }

    private static string CompositeKey<T>(object key) => $"{typeof(T).Name}:{key}";
}
