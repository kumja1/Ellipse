using System.Collections.Concurrent;
using AngleSharp;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Clients;
using Microsoft.Extensions.Caching.Distributed;

namespace Ellipse.Server.Services;

public sealed class SchoolsScraperService(GeocodingService geoService, IDistributedCache cache)
    : IDisposable
{
    private readonly ConcurrentDictionary<int, Task<string>> _tasks = new();

    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(
        Configuration.Default.WithDefaultLoader().WithXPath()
    );

    private const string CacheFolderName = "scraper";

    public async Task<string> ScrapeDivision(int divisionCode, bool forceRefresh = false)
    {
        string? cachedData = await cache.GetStringAsync($"division_{divisionCode}");
        if (!string.IsNullOrEmpty(cachedData) && !forceRefresh)
            return StringHelper.Decompress(cachedData!);

        try
        {
            string result = await _tasks
                .GetOrAdd(divisionCode, _ => ScrapeDivisionInternal(divisionCode))
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(result))
                return string.Empty;

            await cache.SetStringAsync($"division_{divisionCode}", StringHelper.Compress(result));
            return result;
        }
        finally
        {
            _tasks.TryRemove(divisionCode, out _);
        }
    }

    private async Task<string> ScrapeDivisionInternal(int divisionCode)
    {
        SchoolDivisionScraper scraper = new(divisionCode, geoService, _browsingContext);
        return await scraper.Scrape().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _tasks.Clear();
        geoService.Dispose();
    }
}
