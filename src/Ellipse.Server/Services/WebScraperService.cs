using System.Collections.Concurrent;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Clients;

namespace Ellipse.Server.Services;

public sealed class WebScraperService(GeocodingService geoService, SupabaseCache cache)
    : IDisposable
{
    private readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    private const string FolderName = "webscraper_cache";

    public async Task<string> StartNew(int divisionCode, bool overrideCache)
    {
        string cachedData = await cache.Get($"division_{divisionCode}", FolderName);
        if (!string.IsNullOrEmpty(cachedData) && !overrideCache)
            return StringHelper.Decompress(cachedData!);
        try
        {
            string result = await _scrapingTasks
                .GetOrAdd(divisionCode, _ => StartScraper(divisionCode))
                .ConfigureAwait(false);

            await cache.Set($"division_{divisionCode}", StringHelper.Compress(result), FolderName);

            ArgumentException.ThrowIfNullOrEmpty(result, nameof(result));
            return result;
        }
        finally
        {
            _scrapingTasks.TryRemove(divisionCode, out _);
        }
    }

    private async Task<string> StartScraper(int divisionCode)
    {
        WebScraper scraper = new(divisionCode, geoService);
        return await scraper.Scrape().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _scrapingTasks.Clear();
        geoService.Dispose();
    }
}
