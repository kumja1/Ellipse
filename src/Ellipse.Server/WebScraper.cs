using AngleSharp;
using AngleSharp.Dom;
using System.Text.Json;
using System.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using Ellipse.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace Ellipse.Server;

public sealed partial class WebScraper
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";

    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private static readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();
    private static readonly SemaphoreSlim _addressSemaphore = new(20, 20);

    [GeneratedRegex(@"[.\s]+")]
    private static partial Regex CleanNameRegex();

    private readonly int _divisionCode;
    private readonly IBrowsingContext _browsingContext;
    private readonly Dictionary<string, string> _addressCache = [];

    public WebScraper(int divisionCode)
    {
        _divisionCode = divisionCode;
        _browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    }

    public static async Task<string> StartNewAsync(int divisionCode, bool overrideCache)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Starting scrape for division {divisionCode}");
        if (_cache.TryGetValue(divisionCode, out string? cachedData) && !overrideCache)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Cache hit for division {divisionCode}");
            return cachedData!;
        }

        return await _scrapingTasks.GetOrAdd(divisionCode, async _ =>
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] No cache; creating new scraper instance for division {divisionCode}");
                var scraper = new WebScraper(divisionCode);
                string result = await scraper.ScrapeAsync().ConfigureAwait(false);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Scrape completed for division {divisionCode}");

                _cache.Set(divisionCode, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                });
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Result cached for division {divisionCode}");
                return result;
            }
            finally
            {
                _scrapingTasks.TryRemove(divisionCode, out var _);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Removed division {divisionCode} from active tasks");
            }
        }).ConfigureAwait(false);
    }

    public async Task<string> ScrapeAsync()
    {
        var (firstPageSchools, totalPages) = await ProcessPageAsync(1).ConfigureAwait(false);
        var queue = new ConcurrentQueue<SchoolData>(firstPageSchools);

        if (totalPages > 1)
        {
            var pages = Enumerable.Range(2, totalPages - 1);
            await Parallel.ForEachAsync(pages, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (page, _) =>
            {
                var (schools, _) = await ProcessPageAsync(page).ConfigureAwait(false);
                foreach (var school in schools)
                    queue.Enqueue(school);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Processed page {page}, found {schools.Count} schools");
            }).ConfigureAwait(false);
        }

        var allSchools = queue.ToList();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Total schools scraped: {allSchools.Count}");
        return JsonSerializer.Serialize(allSchools);
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var sw = Stopwatch.StartNew();
        var url = $"{BaseUrl}/page/{page}?division={_divisionCode}";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Fetching URL: {url}");

        var document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
        var rows = document.QuerySelectorAll("table > tbody > tr");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Found {rows.Length} rows on page {page}");

        var tasks = rows.Select(ProcessRowAsync);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var schools = results.Where(s => s != null).Cast<SchoolData>().ToList();

        sw.Stop();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Page {page} processed in {sw.ElapsedMilliseconds}ms");
        return (schools, ParseTotalPages(document));
    }

    private async Task<SchoolData?> ProcessRowAsync(IElement row)
    {
        var nameCell = row.QuerySelector("td:first-child");
        if (nameCell == null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Skipping row with missing name cell");
            return null;
        }

        var name = WebUtility.HtmlDecode(nameCell.TextContent).Trim();
        var cleanedName = CleanNameRegex().Replace(name.ToLower(), "-");

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Processing school: {name}");

        var address = await FetchAddressAsync(cleanedName).ConfigureAwait(false);
        var cell2 = row.QuerySelector("td:nth-child(2)")?.TextContent.Trim() ?? "";
        var cell3 = row.QuerySelector("td:nth-child(3)")?.TextContent.Trim() ?? "";

        return new SchoolData(name, cell2, cell3, address, GeoPoint2d.Zero);
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        if (_addressCache.TryGetValue(cleanedName, out var cachedAddress))
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Cache hit for {cleanedName}");
            return cachedAddress;
        }

        await _addressSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var url = $"{SchoolInfoUrl}/{cleanedName}";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Fetching address from {url}");

            var document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
            var addressElement = document.QuerySelector(
                "span[itemprop='address'] span[itemprop='streetAddress'], " +
                "span[itemprop='streetAddress'], " +
                "span[itemtype='http://schema.org/PostalAddress'] span"
            );

            var address = addressElement?.TextContent.Trim() ?? "";
            _addressCache[cleanedName] = address;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Cached address for {cleanedName}");
            return address;
        }
        finally
        {
            _addressSemaphore.Release();
        }
    }

    private static int ParseTotalPages(IDocument document)
    {
        var pages = document.QuerySelectorAll("a.page-numbers")
            .Select(e => int.TryParse(e.TextContent, out var p) ? p : 0)
            .Append(1)
            .Max();

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ParseTotalPages] Found {pages} total pages");
        return pages;
    }
}