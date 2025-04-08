using AngleSharp;
using AngleSharp.Dom;
using System.Text.Json;
using System.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using Ellipse.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using AngleSharp.XPath;
using Ellipse.Server.Services;

namespace Ellipse.Server.Functions;

public sealed partial class WebScraper(int divisionCode, GeoService geoService)
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";

    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private static readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    [GeneratedRegex(@"[.\s/]+")]
    private static partial Regex CleanNameRegex();

    private readonly int _divisionCode = divisionCode;
    private readonly GeoService _geoService = geoService;

    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader().WithXPath());

    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public static async Task<string> StartNewAsync(int divisionCode, bool overrideCache, GeoService geoService)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Starting scrape for division {divisionCode}");
        if (_cache.TryGetValue(divisionCode, out string? cachedData) && !overrideCache)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Cache hit for division {divisionCode}");
            return cachedData!;
        }

        return await _scrapingTasks.GetOrAdd(divisionCode, _ => StartScraperAsync(divisionCode, geoService)).ConfigureAwait(false);
    }

    private static async Task<string> StartScraperAsync(int divisionCode, GeoService geoService)
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] No cache; creating new scraper instance for division {divisionCode}");
            var scraper = new WebScraper(divisionCode, geoService);
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
    }

    public async Task<string> ScrapeAsync()
    {
        var (firstPageSchools, totalPages) = await ProcessPageAsync(1).ConfigureAwait(false);
        var queue = new ConcurrentQueue<SchoolData>(firstPageSchools);

        if (totalPages > 1)
        {
            var pages = Enumerable.Range(2, totalPages - 1);
            await Parallel.ForEachAsync(pages, new ParallelOptions { MaxDegreeOfParallelism = 35 }, async (page, _) =>
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

        IDocument? document = null;
        var rows = await RetryIfInvalid(l => l.Count > 0, async (_) =>
        {
            document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
            return document.QuerySelectorAll("table > tbody > tr").ToList();
        }, []);


        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Found {rows.Count} rows on page {page}");
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

        var cell2 = row.QuerySelector("td:nth-child(2)")?.TextContent.Trim() ?? "";
        var cell3 = row.QuerySelector("td:nth-child(3)")?.TextContent.Trim() ?? "";
        var address = await FetchAddressAsync(cleanedName).ConfigureAwait(false);
        var geoLocation = await RetryIfInvalid(c => c.Lat == 0 || c.Lon == 0, (_) => _geoService.GetLatLngCached(address), GeoPoint2d.Zero, 6);

        return new SchoolData(name, cell2, cell3, address, geoLocation);
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        var address = await RetryIfInvalid(s => !string.IsNullOrWhiteSpace(s), async (attempt) =>
         {
             string address = "";
             
             await _semaphore.WaitAsync().ConfigureAwait(false);
             try
             {
                 var url = $"{SchoolInfoUrl}/{cleanedName}";
                 Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Fetching address from {url} (Attempt {attempt})");

                 var document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
                 if (document == null)
                     Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Html document is null. Url: {url}");

                 var addressElement = document?.QuerySelector(
                     "span[itemprop='streetAddress'], " +
                     "[itemtype='http://schema.org/PostalAddress'] [itemprop='streetAddress'], " +
                     "span[itemprop='address'] > span, " +
                     "[itemtype='http://schema.org/PostalAddress']"
                 );


                 addressElement ??= document?.Body.SelectSingleNode("//strong[contains(text(),'Address')]/following-sibling::*[1]", true) as IElement;
                 address = addressElement?.TextContent.Trim() ?? "";

                 if (string.IsNullOrWhiteSpace(address))
                     Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: Empty address fetched for {cleanedName}. Retrying...");
                 else
                     Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Fetched address: {address} for {cleanedName}");
             }
             catch (Exception ex)
             {
                 Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: Exception occurred for {cleanedName}: {ex.Message}. Retrying...");
             }
             finally
             {
                 _semaphore.Release();
             }

             return address;
         }, "", 4);

        if (string.IsNullOrWhiteSpace(address))
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Failed to get address for {cleanedName}");
        }

        return address;
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

    private static async Task<TResult> RetryIfInvalid<TResult>(Func<TResult, bool> isValid, Func<int, Task<TResult>> action, TResult defaultValue, int maxAttempts = 3, int delay = 2)
    {
        TResult result = defaultValue;
        int attempts = 0;

        while (attempts < maxAttempts && !isValid(result))
        {
            result = await action(attempts);
            if (!isValid(result))
                await Task.Delay(TimeSpan.FromSeconds(delay * 2 ^ attempts)).ConfigureAwait(false);
        }
        return result;
    }

}
