using HtmlAgilityPack;
using System.Text;
using System.Text.Json;
using Ellipse.Common.Models;

namespace Ellipse.Server;

public sealed class WebScraper(int divisionCode)
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";
    private static readonly HttpClient _httpClient = new();
    private static readonly Dictionary<int, string> _cache = [];

    private static readonly SemaphoreSlim _semaphore = new(5, 20);
    private readonly int _divisionCode = divisionCode;

    public static async Task<string> StartNewAsync(int divisionCode)
    {
        if (!_cache.TryGetValue(divisionCode, out var results))
        {
            var scraper = new WebScraper(divisionCode);
            results = await scraper.ScrapeAsync();
        }

        Console.WriteLine("Using Cached Result");
        return results;
    }

    public async Task<string> ScrapeAsync()
    {
        using var memoryStream = new MemoryStream();
        await using var writer = new Utf8JsonWriter(memoryStream);

        writer.WriteStartArray();
        var results = new List<SchoolData>();

        int page = 1, totalPages = 1;
        var pageTasks = new List<Task<(List<SchoolData> Schools, int TotalPages)>>();

        do
        {
            pageTasks.Add(ProcessPageAsync(page));
            page++;
        } while (page <= totalPages);

        var pageResults = await Task.WhenAll(pageTasks);
        foreach (var schools in pageResults.SelectMany(s => s.Schools))
        {
            JsonSerializer.Serialize(writer, schools);
        }

        writer.WriteEndArray(); 
        await writer.FlushAsync();
        var jsonString = Encoding.UTF8.GetString(memoryStream.ToArray());
        _cache[_divisionCode] = jsonString;
        return jsonString;
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var url = $"{BaseUrl}/page/{page}?division={_divisionCode}&filter=";
        var doc = new HtmlDocument();
        doc.LoadHtml(await FetchPage(url));
        return (Schools: await ExtractSchoolData(doc), TotalPages: ParseTotalPages(doc));
    }

    private async Task<List<SchoolData>> ExtractSchoolData(HtmlDocument doc)
    {
        var schools = new List<SchoolData>();
        var rows = doc.DocumentNode.SelectNodes("//table/tbody/tr") ?? new HtmlNodeCollection(null);

        var tasks = rows.Select(async row =>
        {
              try {
                await _semaphore.WaitAsync();
                var name = row.SelectSingleNode("td[1]")?.InnerText.Trim() ?? "";
                var detailUrl = $"{SchoolInfoUrl}/{name.Replace(' ', '-').ToLower()}";
                var schoolInfoDoc = new HtmlDocument();
                schoolInfoDoc.LoadHtml(await FetchPage(detailUrl));

                var address = schoolInfoDoc.DocumentNode
                    .SelectSingleNode("//span[@itemprop='streetAddress']")?.InnerText.Trim() ?? "";

                return new SchoolData(
                    name,
                    row.SelectSingleNode("td[2]")?.InnerText.Trim() ?? "",
                    row.SelectSingleNode("td[3]")?.InnerText.Trim() ?? "",
                    address,
                    GeoPoint2d.Zero
                );} finally {
                    _semaphore.Release();
                }
        }).ToList();

        schools.AddRange(await Task.WhenAll(tasks));
        return schools;
    }

    private int ParseTotalPages(HtmlDocument doc)
    {
        var pageNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'pagination')]//a[@class='page-numbers']");
        if (pageNodes == null) return 1;

        return pageNodes
            .Select(node => int.TryParse(node.InnerText, out var pageNumber) ? pageNumber : 0)
            .DefaultIfEmpty(1)
            .Max();
    }

    private static async Task<string> FetchPage(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch {url}: {response.StatusCode}");
                return "";
            }
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Network error while fetching {url}: {e.Message}");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"Timeout while fetching {url}");
        }
        return "";
    }



}
