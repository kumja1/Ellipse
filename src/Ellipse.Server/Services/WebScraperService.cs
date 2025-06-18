using System.Collections.Concurrent;
using Ellipse.Server.Utils.Clients;
using Ellipse.Server.Utils.Helpers;
using WebScraper = Ellipse.Server.Utils.WebScraper;

namespace Ellipse.Server.Services;

public sealed class WebScraperService(GeoService geoService, SupabaseStorageClient storageClient)
{
    private readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    private const string FolderName = "webscraper_cache";

    public async Task<string> StartNewAsync(int divisionCode, bool overrideCache)
    {
        string cachedData = await storageClient.Get($"division_{divisionCode}", FolderName);
        if (!string.IsNullOrEmpty(cachedData) && !overrideCache)
            return StringHelper.DecompressString(cachedData!);
        
        var task = _scrapingTasks.GetOrAdd(divisionCode, _ => StartScraperAsync(divisionCode));
        string result = await task.ConfigureAwait(false);
        
        ArgumentException.ThrowIfNullOrEmpty(result, nameof(result));
        return result;
    }

    private async Task<string> StartScraperAsync(int divisionCode)
    {
        try
        {
            WebScraper scraper = new WebScraper(divisionCode, geoService);
            string result = await scraper.ScrapeAsync().ConfigureAwait(false);

            await storageClient.Set(
                $"division_{divisionCode}",
                StringHelper.CompressString(result),
                FolderName
            );
            return result;
        }
        finally
        {
            _scrapingTasks.TryRemove(divisionCode, out _);
        }
    }
}
