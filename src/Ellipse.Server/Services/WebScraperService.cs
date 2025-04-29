using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Objects;

namespace Ellipse.Server.Services;

public sealed class WebScraperService
{
    private readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    public async Task<string> StartNewAsync(
        int divisionCode,
        bool overrideCache,
        GeoService geoService,
        SupabaseStorageClient storageClient)
    {
        if (
            SingletonMemoryCache.TryGetEntry<WebScraperService, string>(
                divisionCode,
                out string? cachedData
            ) && !overrideCache
        )
        {
            return StringCompressor.DecompressString(cachedData!);
        }

        var task = _scrapingTasks.GetOrAdd(
            divisionCode,
            _ => StartScraperAsync(divisionCode, geoService)
        );
        var result = await task.ConfigureAwait(false);
        return result;
    }

    private async Task<string> StartScraperAsync(int divisionCode, GeoService geoService)
    {
        try
        {
            var scraper = new WebScraper(divisionCode, geoService);
            string result = await scraper.ScrapeAsync().ConfigureAwait(false);

            SingletonMemoryCache.SetEntry<WebScraperService, string>(
                divisionCode,
                StringCompressor.CompressString(result),
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                    SlidingExpiration = TimeSpan.FromDays(10),
                }
            );

            return result;
        }
        catch
        {
            throw;
        }
        finally
        {
            _scrapingTasks.TryRemove(divisionCode, out _);
        }
    }
}
