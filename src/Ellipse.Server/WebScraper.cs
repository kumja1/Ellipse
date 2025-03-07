using HtmlAgilityPack;
using System.Text;
using System.Text.Json;
using System.Net;
using Ellipse.Common.Models;
using Ellipse.Server.Extensions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Buffers;

namespace Ellipse.Server;

public sealed class WebScraper(int divisionCode)
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";
    private static readonly HttpClient _httpClient = new(new HttpClientHandler { MaxConnectionsPerServer = 20 });
    private static readonly ConcurrentDictionary<int, string> _cache = [];

    private readonly int _divisionCode = divisionCode;

    public static async Task<string> StartNewAsync(int divisionCode)
    {
        return await _cache.GetOrAddAsync(divisionCode, async code =>
        {
            var scraper = new WebScraper(code);
            return await scraper.ScrapeAsync();
        });
    }

    public async Task<string> ScrapeAsync()
    {
        var (schools, totalPages) = await ProcessPageAsync(1);
        var allSchools = new List<SchoolData>(schools);

        if (totalPages > 1)
        {
            var remainingTasks = Enumerable.Range(2, totalPages - 1)
                .Select(ProcessPageAsync);
            var remainingResults = await Task.WhenAll(remainingTasks);
            allSchools.AddRange(remainingResults.SelectMany(r => r.Schools));
        }

        return JsonSerializer.Serialize(allSchools);
    }


    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var url = $"{BaseUrl}/page/{page}?division={_divisionCode}&filter=";
        var html = await FetchPage(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var schools = await ExtractSchoolData(doc);
        var totalPages = ParseTotalPages(doc);
        return (schools, totalPages);
    }

    private async Task<List<SchoolData>> ExtractSchoolData(HtmlDocument doc)
    {
        var rows = doc.DocumentNode.SelectNodes("//table/tbody/tr") ?? new HtmlNodeCollection(null);
        var tasks = rows.Select(ProcessRowAsync).ToList();
        var results = await Task.WhenAll(tasks);
        return results.Where(s => s != null).Cast<SchoolData>().ToList();
    }

    private async Task<SchoolData?> ProcessRowAsync(HtmlNode row)
    {
        var nameCell = row.SelectSingleNode("td[1]");
        if (nameCell == null) return null;

        var name = WebUtility.HtmlDecode(nameCell.InnerText).Trim();
        var cleanedName = name.ToLower().Replace(" ", "-");

        var address = await FetchAddressAsync(cleanedName);

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
        var infoDoc = new HtmlDocument();
        infoDoc.LoadHtml(await FetchPage($"{SchoolInfoUrl}/{cleanedName}"));
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
                using var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode >= 500)
                    await Task.Delay(retryDelay);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (i == maxRetries - 1) break;
                await Task.Delay(retryDelay);
                retryDelay *= 2;
            }
        }
        return string.Empty;
    }
}
