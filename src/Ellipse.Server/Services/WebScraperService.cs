using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Ellipse.Common.Models;
using Ellipse.Server.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace Ellipse.Server.Services;

public sealed partial class WebScraperService
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";

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
        if (
            SingletonMemoryCache.TryGetEntry<WebScraperService, string>(
                divisionCode,
                out string? cachedData
            ) && !overrideCache
        )
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [StartNewAsync] Cache hit for division {divisionCode}"
            );

            return StringCompressor.DecompressString(cachedData!);
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

            SingletonMemoryCache.SetEntry<WebScraperService, string>(
                divisionCode,
                StringCompressor.CompressString(result),
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                    SlidingExpiration = TimeSpan.FromDays(10),
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
        try
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
                        new ParallelOptions { MaxDegreeOfParallelism = 28 },
                        async (page, _) =>
                        {
                            try
                            {
                                Console.WriteLine(
                                    $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Starting processing for page {page}"
                                );
                                var (schools, _) = await ProcessPageAsync(page)
                                    .ConfigureAwait(false);
                                foreach (var school in schools)
                                    queue.Enqueue(school);

                                Console.WriteLine(
                                    $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Processed page {page}, found {schools.Count} schools"
                                );
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(
                                    $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Exception processing page {page}: {ex}"
                                );
                            }
                        }
                    )
                    .ConfigureAwait(false);
            }

            sw.Stop();
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Completed scraping Division {_divisionCode} in {sw.ElapsedMilliseconds}ms. Total schools scraped: {queue.Count}"
            );
            return JsonSerializer.Serialize(queue.ToList());
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ScrapeAsync] Unhandled exception: {ex}"
            );
            throw;
        }
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        try
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
                        try
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
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Exception loading document: {ex}"
                            );
                            return [];
                        }
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
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessPageAsync] Exception on page {page}: {ex}"
            );
            return ([], 1); // Default to page 1 only
        }
    }

    private async Task<SchoolData?> ProcessRowAsync(IElement row)
    {
        try
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

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Fetching address for: {name}"
            );
            var address = await FetchAddressAsync(cleanedName).ConfigureAwait(false);
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Address fetched for {name}: {address}"
            );

            var geoLocation = await RequestHelper
                .RetryIfInvalid(
                    isValid: c => c != GeoPoint2d.Zero,
                    func: async (attempt) =>
                    {
                        try
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Attempt {attempt}: Fetching coordinates for {name} with address: {address}"
                            );
                            var location = await _geoService
                                .GetLatLngCached(address)
                                .ConfigureAwait(false);
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Attempt {attempt}: Received coordinates: {location} for {name}"
                            );
                            return location;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Attempt {attempt}: Failed to fetch coordinates for {name}. Exception: {ex}"
                            );
                            return GeoPoint2d.Zero;
                        }
                    },
                    defaultValue: GeoPoint2d.Zero,
                    maxRetries: 5,
                    delayMs: 500
                )
                .ConfigureAwait(false);

            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Geopoint Location: {geoLocation}"
            );
            return new SchoolData(name, address, cell2, cell3, geoLocation);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessRowAsync] Exception for row: {ex}"
            );
            return null;
        }
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        try
        {
            string address = await RequestHelper
                .RetryIfInvalid(
                    isValid: s => !string.IsNullOrEmpty(s),
                    func: async (attempt) =>
                    {
                        try
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
                                    $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Requesting: {url}"
                                );
                                var doc = await _browsingContext
                                    .OpenAsync(url)
                                    .ConfigureAwait(false);
                                var el = doc.QuerySelector(
                                    "[itemtype='http://schema.org/PostalAddress']"
                                );

                                fetchedAddress = WebUtility
                                    .HtmlDecode(el?.TextContent ?? "")
                                    .Trim();
                            }
                            finally
                            {
                                _semaphore.Release();
                            }
                            return fetchedAddress;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Error fetching from page: {ex}"
                            );
                            return string.Empty;
                        }
                    },
                    defaultValue: "",
                    maxRetries: 10,
                    delayMs: 200
                )
                .ConfigureAwait(false);

            return address;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [FetchAddressAsync] Fatal error: {ex}"
            );
            return string.Empty;
        }
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
