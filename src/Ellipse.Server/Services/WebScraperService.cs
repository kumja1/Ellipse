using System.Collections.Concurrent;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Helpers;
using Ellipse.Server.Utils.Objects;
using Microsoft.Extensions.Caching.Memory;

namespace Ellipse.Server.Services;

public sealed class WebScraperService(GeoService geoService, SupabaseStorageClient storageClient)
{
    private readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    public async Task<string> StartNewAsync(int divisionCode, bool overrideCache)
    {
        string? cachedData = await storageClient.Get(divisionCode);
        if (!string.IsNullOrEmpty(cachedData) && !overrideCache)
            return StringCompressor.DecompressString(cachedData!);

        var task = _scrapingTasks.GetOrAdd(divisionCode, _ => StartScraperAsync(divisionCode));
        var result = await task.ConfigureAwait(false);
        return result;
    }

    private async Task<string> StartScraperAsync(int divisionCode)
    {
        try
        {
            var scraper = new WebScraper(divisionCode, geoService);
            string result = await scraper.ScrapeAsync().ConfigureAwait(false);

            await storageClient.Set(divisionCode, StringCompressor.CompressString(result));
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
