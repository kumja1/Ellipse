using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.XPath;
using Ellipse.Common.Models;
using Ellipse.Server.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace Ellipse.Server.Services;

public sealed partial class WebScraperService
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";

    private static readonly MemoryCache _cache = new(new MemoryCacheOptions() { });
    private static readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    [GeneratedRegex(@"[.\s/]+")]
    private static partial Regex CleanNameRegex();

    private readonly int _divisionCode;
    private readonly GeoService _geoService;
    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(
        Configuration.Default.WithDefaultLoader().WithXPath()
    );
    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public WebScraperService(int divisionCode, GeoService geoService)
    {
        _divisionCode = divisionCode;
        _geoService = geoService;
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [WebScraper.ctor] Created scraper instance for Division {_divisionCode}"
        );
    }

    public static async Task<string> StartNewAsync(
        int divisionCode,
        bool overrideCache,
        GeoService geoService
    )
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Starting scrape for division {divisionCode}"
        );
        if (_cache.TryGetValue(divisionCode, out string? cachedData) && !overrideCache)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Cache hit for division {divisionCode}"
            );
            return cachedData!;
        }

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] No cache or override enabled for division {divisionCode}"
        );
        var task = _scrapingTasks.GetOrAdd(
            divisionCode,
            _ => StartScraperAsync(divisionCode, geoService)
        );
        var result = await task.ConfigureAwait(false);
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Returning result for division {divisionCode}"
        );
        return result;
    }

    private static async Task<string> StartScraperAsync(int divisionCode, GeoService geoService)
    {
        try
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [StartScraperAsync] Creating new scraper instance for Division {divisionCode}"
            );
            var scraper = new WebScraperService(divisionCode, geoService);
            string result = await scraper.ScrapeAsync().ConfigureAwait(false);
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [StartScraperAsync] Scrape completed for Division {divisionCode}"
            );

            _cache.Set(
                divisionCode,
                result,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                    SlidingExpiration = TimeSpan.FromDays(7),
                }
            );
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [StartScraperAsync] Result cached for Division {divisionCode}"
            );
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [StartScraperAsync] Exception for Division {divisionCode}: {ex}"
            );
            throw;
        }
        finally
        {
            _scrapingTasks.TryRemove(divisionCode, out _);
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [StartScraperAsync] Removed Division {divisionCode} from active tasks"
            );
        }
    }

    private async Task<string> ScrapeAsync()
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Beginning scrape for Division {_divisionCode}"
        );
        var sw = Stopwatch.StartNew();

        var (firstPageSchools, totalPages) = await ProcessPageAsync(1).ConfigureAwait(false);
        var queue = new ConcurrentQueue<SchoolData>(firstPageSchools);
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Processed first page for Division {_divisionCode} - Total pages: {totalPages}"
        );

        if (totalPages > 1)
        {
            await Parallel
                .ForEachAsync(
                    Enumerable.Range(2, totalPages - 1),
                    new ParallelOptions { MaxDegreeOfParallelism = 32 },
                    async (page, _) =>
                    {
                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Starting processing for page {page}"
                        );
                        var (schools, _) = await ProcessPageAsync(page).ConfigureAwait(false);
                        foreach (var school in schools)
                            queue.Enqueue(school);

                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Processed page {page}, found {schools.Count} schools"
                        );
                    }
                )
                .ConfigureAwait(false);
        }

        var allSchools = queue.ToList();
        sw.Stop();
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Completed scraping Division {_divisionCode} in {sw.ElapsedMilliseconds}ms. Total schools scraped: {allSchools.Count}"
        );
        return JsonSerializer.Serialize(allSchools);
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var sw = Stopwatch.StartNew();
        var url = $"{BaseUrl}/page/{page}?division={_divisionCode}";
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Fetching URL: {url} for page {page}"
        );

        IDocument? document = null;
        List<IElement> rows = await RequestHelper
            .RetryIfInvalid(
                isValid: l => l.Count > 0,
                func: async (_) =>
                {
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Attempting to load document for URL: {url}"
                    );
                    document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
                    var rowList = document.QuerySelectorAll("table > tbody > tr").ToList();
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Found {rowList.Count} rows in document for page {page}"
                    );
                    return rowList;
                },
                defaultValue: [],
                maxRetries: 10,
                delayMs: 300
            )
            .ConfigureAwait(false);

        var tasks = rows.Select(ProcessRowAsync);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var schools = results.Where(s => s != null).Cast<SchoolData>().ToList();

        sw.Stop();
        int totalPages = ParseTotalPages(document!);
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Completed processing page {page} in {sw.ElapsedMilliseconds}ms - TotalPages: {totalPages}"
        );
        return (schools, totalPages);
    }

    private async Task<SchoolData?> ProcessRowAsync(IElement row)
    {
        var nameCell = row.QuerySelector("td:first-child");
        if (nameCell == null)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Skipping row with missing name cell."
            );
            return null;
        }

        var name = WebUtility.HtmlDecode(nameCell.TextContent).Trim();
        var cleanedName = CleanNameRegex().Replace(name.ToLower(), "-");

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Processing school: {name} (Cleaned: {cleanedName})"
        );

        var cell2 = row.QuerySelector("td:nth-child(2)")?.TextContent.Trim() ?? "";
        var cell3 = row.QuerySelector("td:nth-child(3)")?.TextContent.Trim() ?? "";
        var address = await FetchAddressAsync(cleanedName).ConfigureAwait(false);
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Fetched address for {name}: {address}"
        );

        var geoLocation = await RequestHelper
            .RetryIfInvalid(
                isValid: c => c != GeoPoint2d.Zero,
                func: async (attempt) =>
                {
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Attempt {attempt}: Fetching coordinates for {name} with address: {address}"
                    );
                    var location = await _geoService.GetLatLngCached(address).ConfigureAwait(false);
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Attempt {attempt}: Received coordinates: {location} for {name}"
                    );
                    return location;
                },
                defaultValue: GeoPoint2d.Zero,
                maxRetries: 10,
                delayMs: 50
            )
            .ConfigureAwait(false);

        if (geoLocation == GeoPoint2d.Zero)
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Failed to get coordinates for school {name}"
            );

        return new SchoolData(name, cell2, cell3, address, geoLocation);
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        string address = await RequestHelper
            .RetryIfInvalid(
                isValid: s => !string.IsNullOrEmpty(s),
                func: async (attempt) =>
                {
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: Fetching address for {cleanedName}"
                    );
                    string fetchedAddress = string.Empty;
                    await _semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var url = $"{SchoolInfoUrl}/{cleanedName}";
                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: Opening URL: {url}"
                        );
                        var document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);

                        if (document == null)
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: Document is null for URL: {url}"
                            );

                        var addressElement = document?.QuerySelector(
                            "span[itemprop='streetAddress'], "
                                + "[itemtype='http://schema.org/PostalAddress'] [itemprop='streetAddress'], "
                                + "span[itemprop='address'] > span, "
                                + "[itemtype='http://schema.org/PostalAddress']"
                        );

                        // Fallback via XPath if needed
                        addressElement ??=
                            document?.Body.SelectSingleNode(
                                "//strong[contains(text(),'Address')]/following-sibling::*[1]",
                                true
                            ) as IElement;

                        fetchedAddress = addressElement?.TextContent.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(fetchedAddress))
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: No address found at URL: {url}"
                            );
                        }
                        else
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: Fetched address: {fetchedAddress} for {cleanedName}"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Attempt {attempt}: Exception occurred for {cleanedName}: {ex}"
                        );
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                    return fetchedAddress;
                },
                defaultValue: "",
                maxRetries: 10,
                delayMs: 50
            )
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(address))
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Final failure: Unable to obtain address for {cleanedName}"
            );
        }
        return address;
    }

    private static int ParseTotalPages(IDocument document)
    {
        int totalPages = document
            .QuerySelectorAll("a.page-numbers")
            .Select(e => int.TryParse(e.TextContent, out var p) ? p : 0)
            .Append(1)
            .Max();

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ParseTotalPages] Determined total pages: {totalPages}"
        );
        return totalPages;
    }
}
