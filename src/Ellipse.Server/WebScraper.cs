using HtmlAgilityPack;
using System.Text.Json;
using Ellipse.Common.Models;

namespace Ellipse.Server;

public sealed class WebScraper(int divisionCode)
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";

    private const string SchoolInfo = "https://schoolquality.virginia.gov/schools";

    private readonly HtmlWeb HtmlWeb = new();
    private static readonly Dictionary<int, string> _cache = [];
    private readonly int _divisionCode = divisionCode;

    public static async Task<string> StartNewAsync(int divisionCode)
    {
        if (!_cache.TryGetValue(divisionCode, out var results))
        {
            var scraper = new WebScraper(divisionCode);
            results = await scraper.ScrapeAsync();
        }

        return results;
    }

    public async Task<string> ScrapeAsync()
    {
        var results = new List<SchoolData>();
        int page = 1, totalPages;

        do
        {
            var (schools, pages) = await ProcessPageAsync(page);
            results.AddRange(schools);
            totalPages = pages;
            page++;
        } while (page <= totalPages);

        var resultsString = JsonSerializer.Serialize(results);
        _cache[_divisionCode] = resultsString;
        return resultsString;
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var doc = await HtmlWeb.LoadFromWebAsync($"{BaseUrl}/page/{page}?division={_divisionCode}&filter=");
        var schools = await ExtractSchoolData(doc);
        var totalPages = ParseTotalPages(doc);

        return (schools, totalPages);
    }

    private async Task<List<SchoolData>> ExtractSchoolData(HtmlDocument doc)
    {
        var schools = new List<SchoolData>();
        var rows = doc.DocumentNode.SelectNodes("//table/tbody/tr") ?? new HtmlNodeCollection(null);


        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = row.SelectSingleNode("td[1]")?.InnerText.Trim() ?? "";
            var schoolInfo = await HtmlWeb.LoadFromWebAsync($"{SchoolInfo}/${name}");

            schools.Add(new SchoolData(
                name,
                row.SelectSingleNode("td[2]")?.InnerText.Trim() ?? "",
                row.SelectSingleNode("td[3]")?.InnerText.Trim() ?? "",
                schoolInfo.DocumentNode.SelectSingleNode("//span[@itemprop='address']")?.InnerText.Trim() ?? "",
                GeoPoint2d.Zero)
            );
        }

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
}
