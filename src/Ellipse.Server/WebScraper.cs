using AngleSharp;
using AngleSharp.Dom;
using System.Text.Json;
using System.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using Ellipse.Common.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Ellipse.Server;

public sealed class WebScraper
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";

    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private static readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    private readonly int _divisionCode;
    private readonly IBrowsingContext _browsingContext;

    public WebScraper(int divisionCode)
    {
        _divisionCode = divisionCode;

        _browsingContext = BrowsingContext.New(
            Configuration
            .Default
            .WithDefaultLoader()
        );
    }

    public static async Task<string> StartNewAsync(int divisionCode)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Starting scrape for division {divisionCode}");
        if (_cache.TryGetValue(divisionCode, out string? cachedData))
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

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                };
                _cache.Set(divisionCode, result, cacheEntryOptions);
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
            await Parallel.ForEachAsync(pages, new ParallelOptions { MaxDegreeOfParallelism = 30 }, async (page, token) =>
            {
                var (schools, _) = await ProcessPageAsync(page).ConfigureAwait(false);
                foreach (var school in schools)
                {
                    queue.Enqueue(school);
                }
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Processed page {page}, found {schools.Count} schools");
            }).ConfigureAwait(false);
        }

        var allSchools = queue.ToList();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Total schools scraped: {allSchools.Count}");
        return JsonSerializer.Serialize(allSchools);
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var pageSw = Stopwatch.StartNew();
        var url = $"{BaseUrl}/page/{page}?division={_divisionCode}";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Fetching URL: {url}");

        var document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Fetched HTML for page {page}");

        var rows = document.QuerySelectorAll("table > tbody > tr");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ExtractSchoolData] Found {rows.Length} rows in table");

        var tasks = rows.Select(ProcessRowAsync);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var schoolList = results.Where(s => s != null).Cast<SchoolData>().ToList();

        int totalPages = ParseTotalPages(document);
        pageSw.Stop();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Page {page}: Processed in {pageSw.ElapsedMilliseconds} ms, Found {schoolList.Count} schools; Total pages: {totalPages}");
        return (schoolList, totalPages);
    }

    private async Task<SchoolData?> ProcessRowAsync(IElement row)
    {
        var nameCell = row.QuerySelector("td:nth-of-type(1)");
        if (nameCell == null)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] No name cell found; skipping row");
            return null;
        }

        var name = WebUtility.HtmlDecode(nameCell.TextContent).Trim();
        var cleanedName = name.ToLower().Replace(" ", "-");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Processing school: {name} (cleaned: {cleanedName})");

        var addressTask = FetchAddressAsync(cleanedName).ConfigureAwait(false);

        var cell2 = row.QuerySelector("td:nth-of-type(2)")?.TextContent.Trim() ?? "";
        var cell3 = row.QuerySelector("td:nth-of-type(3)")?.TextContent.Trim() ?? "";

        return new SchoolData(
            name,
            cell2,
            cell3,
            await addressTask,
            GeoPoint2d.Zero
        );
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        var url = $"{SchoolInfoUrl}/{cleanedName}";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Fetching address from URL: {url}");

        var document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Fetched address HTML for {cleanedName} ");

        var addressElement = document.QuerySelector("span[itemprop='streetAddress'], span[itemtype='http://schema.org/PostalAddress'] span");
        var address = addressElement?.TextContent.Trim() ?? "";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Parsed address: {address}");
        return address;
    }

    private static int ParseTotalPages(IDocument document)
    {
        var paginationElements = document.QuerySelectorAll("a.page-numbers");
        int totalPages = paginationElements
            .Select(e => int.TryParse(e.TextContent, out var p) ? p : 0)
            .DefaultIfEmpty(1)
            .Max();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ParseTotalPages] Total pages determined: {totalPages}");
        return totalPages;
    }
}
