using System.Collections.Concurrent;

namespace Ellipse.Server.Extensions;

public static class ConcurrentDictionaryExtensions
{
    public static async Task<TValue> GetOrAddAsync<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> cache, TKey key, Func<TKey, Task<TValue>> valueFactory)
    {
        if (!cache.TryGetValue(key, out var value))
        {
            value = await valueFactory(key);
            cache[key] = value;
        }
        return value;
    }
}