using HtmlAgilityPack;
using System.Text.Json;
using System.Net;
using System.Collections.Concurrent;
using Ellipse.Common.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Ellipse.Server;

public sealed class WebScraper(int divisionCode)
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";
    private static readonly HttpClient _httpClient = new(new HttpClientHandler { MaxConnectionsPerServer = 20 });
    private static readonly SemaphoreSlim _semaphore = new(20, 20);
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private static readonly ConcurrentDictionary<int, Task<string>> _scrapingTasks = new();

    private readonly int _divisionCode = divisionCode;

    public static async Task<string> StartNewAsync(int divisionCode)
    {
        if (_cache.TryGetValue(divisionCode, out string? cachedData))
        {
            return cachedData!;
        }

        return await _scrapingTasks.GetOrAdd(divisionCode, async _ =>
        {
            try
            {
                var scraper = new WebScraper(divisionCode);
                string result = await scraper.ScrapeAsync().ConfigureAwait(false);

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                };
                _cache.Set(divisionCode, result, cacheEntryOptions);
                return result;
            }
            finally
            {
                _scrapingTasks.TryRemove(divisionCode, out var _);
            }
        }).ConfigureAwait(false);
    }

    public async Task<string> ScrapeAsync()
    {
        var (firstPageSchools, totalPages) = await ProcessPageAsync(1).ConfigureAwait(false);
        var allSchools = new List<SchoolData>(firstPageSchools);

        if (totalPages > 1)
        {
            var pageTasks = Enumerable.Range(2, totalPages - 1)
                .Select(async page =>
                {
                    await _semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var (schools, _) = await ProcessPageAsync(page).ConfigureAwait(false);
                        return schools;
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });

            var results = await Task.WhenAll(pageTasks).ConfigureAwait(false);
            foreach (var pageSchools in results)
            {
                allSchools.AddRange(pageSchools);
            }
        }

        return JsonSerializer.Serialize(allSchools);
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var url = $"{BaseUrl}/page/{page}?division={_divisionCode}&filter=";
        var html = await FetchPage(url).ConfigureAwait(false);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var schools = await ExtractSchoolData(doc).ConfigureAwait(false);
        var totalPages = ParseTotalPages(doc);
        return (schools, totalPages);
    }

    private async Task<List<SchoolData>> ExtractSchoolData(HtmlDocument doc)
    {
        var rows = doc.DocumentNode.SelectNodes("//table/tbody/tr") ?? new HtmlNodeCollection(null);
        var tasks = rows.Select(ProcessRowAsync).ToList();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Where(s => s != null).Cast<SchoolData>().ToList();
    }

    private async Task<SchoolData?> ProcessRowAsync(HtmlNode row)
    {
        var nameCell = row.SelectSingleNode("td[1]");
        if (nameCell == null) return null;

        var name = WebUtility.HtmlDecode(nameCell.InnerText).Trim();
        var cleanedName = name.ToLower().Replace(" ", "-");

        var address = await FetchAddressAsync(cleanedName).ConfigureAwait(false);

        return new SchoolData(
            name,
            row.SelectSingleNode("td[2]")?.InnerText.Trim() ?? "",
            row.SelectSingleNode("td[3]")?.InnerText.Trim() ?? "",
            address,
            GeoPoint2d.Zero
        );
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        var infoHtml = await FetchPage($"{SchoolInfoUrl}/{cleanedName}").ConfigureAwait(false);
        var infoDoc = new HtmlDocument();
        infoDoc.LoadHtml(infoHtml);
        var address = infoDoc.DocumentNode
            .SelectSingleNode("//span[@itemprop='streetAddress']")?.InnerText.Trim() ?? "";

        return address;
    }

    private static int ParseTotalPages(HtmlDocument doc)
    {
        var paginationNodes = doc.DocumentNode.SelectNodes("//a[contains(@class, 'page-numbers')]");
        return paginationNodes?.Select(n => int.TryParse(n.InnerText, out var page) ? page : 0)
            .DefaultIfEmpty(1)
            .Max() ?? 1;
    }

    private static async Task<string> FetchPage(string url)
    {
        const int maxRetries = 3;
        int retryDelay = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if ((int)response.StatusCode >= 500)
                    await Task.Delay(retryDelay).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (i == maxRetries - 1) break;
                await Task.Delay(retryDelay).ConfigureAwait(false);
                retryDelay *= 2;
            }
        }
        return string.Empty;
    }
}
