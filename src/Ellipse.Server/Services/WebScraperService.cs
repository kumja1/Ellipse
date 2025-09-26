using System.Collections.Concurrent;
using AngleSharp;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Clients;

namespace Ellipse.Server.Services;

public sealed class WebScraperService(GeocodingService geoService, SupabaseCache cache)
    : IDisposable
{
    private readonly ConcurrentDictionary<int, Task<string>> _tasks = new();

    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(
        Configuration.Default.WithDefaultLoader().WithXPath()
    );

    private const string CacheFolderName = "scraper";

    public async Task<string> StartNew(int divisionCode, bool overrideCache)
    {
        string cachedData = await cache.Get($"division_{divisionCode}", CacheFolderName);
        if (!string.IsNullOrEmpty(cachedData) && !overrideCache)
            return StringHelper.Decompress(cachedData!);

        try
        {
            string result = await _tasks
                .GetOrAdd(divisionCode, StartScraper(divisionCode))
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(result))
                return string.Empty;

            await cache.Set(
                $"division_{divisionCode}",
                StringHelper.Compress(result),
                CacheFolderName
            );
            return result;
        }
        finally
        {
            _tasks.TryRemove(divisionCode, out _);
        }
    }

    private async Task<string> StartScraper(int divisionCode)
    {
        WebScraper scraper = new(divisionCode, geoService, _browsingContext);
        return await scraper.Scrape().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _tasks.Clear();
        geoService.Dispose();
    }
}
