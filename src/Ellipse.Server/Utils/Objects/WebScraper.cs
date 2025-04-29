using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Ellipse.Common.Models;
using Ellipse.Server.Utils;

namespace Ellipse.Server.Utils.Objects;

public sealed partial class WebScraper
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";

    [GeneratedRegex(@"[.\s/]+")]
    private static partial Regex CleanNameRegex();

    private readonly int _divisionCode;
    private readonly GeoService _geoService;
    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(
        Configuration.Default.WithDefaultLoader().WithXPath()
    );

    public WebScraper(int divisionCode, GeoService geoService)
    {
        _divisionCode = divisionCode;
        _geoService = geoService;
    }

    public async Task<string> ScrapeAsync()
    {
        var sw = Stopwatch.StartNew();
        var (firstPageSchools, totalPages) = await ProcessPageAsync(1).ConfigureAwait(false);
        var queue = new ConcurrentQueue<SchoolData>(firstPageSchools);

        if (totalPages > 1)
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(2, totalPages - 1),
                new ParallelOptions { MaxDegreeOfParallelism = 28 },
                async (page, _) =>
                {
                    var (schools, _) = await ProcessPageAsync(page).ConfigureAwait(false);
                    foreach (var school in schools)
                        queue.Enqueue(school);
                }
            );
        }

        sw.Stop();
        return JsonSerializer.Serialize(queue.ToList());
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var url = $"{BaseUrl}/page/{page}?division={_divisionCode}";
        IDocument? document = null;
        List<IElement> rows = await RequestHelper.RetryIfInvalid(
            isValid: l => l.Count > 0,
            func: async _ =>
            {
                document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
                return document.QuerySelectorAll("table > tbody > tr").ToList();
            },
            defaultValue: [],
            maxRetries: 10,
            delayMs: 300
        ).ConfigureAwait(false);

        var tasks = rows.Select(ProcessRowAsync);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var schools = results.Where(s => s != null).Cast<SchoolData>().ToList();
        int totalPages = ParseTotalPages(document!);
        return (schools, totalPages);
    }

    private async Task<SchoolData?> ProcessRowAsync(IElement row)
    {
        var nameCell = row.QuerySelector("td:first-child");
        if (nameCell == null) return null;

        var name = WebUtility.HtmlDecode(nameCell.TextContent).Trim();
        var cleanedName = CleanNameRegex().Replace(name.ToLower(), "-");

        var cell2 = row.QuerySelector("td:nth-child(2)")?.TextContent.Trim() ?? "";
        var cell3 = row.QuerySelector("td:nth-child(3)")?.TextContent.Trim() ?? "";

        var address = await FetchAddressAsync(cleanedName).ConfigureAwait(false);
        var geoLocation = await RequestHelper.RetryIfInvalid(
            isValid: c => c != GeoPoint2d.Zero,
            func: async _ => await _geoService.GeocodeAsync(address),
            defaultValue: GeoPoint2d.Zero
        ).ConfigureAwait(false);

        return new SchoolData
        {
            Name = name,
            Address = address,
            Location = geoLocation,
            ExtraInfo1 = cell2,
            ExtraInfo2 = cell3
        };
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        var addressUrl = $"https://schoolquality.virginia.gov/schools/{cleanedName}";
        var doc = await _browsingContext.OpenAsync(addressUrl).ConfigureAwait(false);
        var el = doc.QuerySelector(".school-address");
        return el?.TextContent.Trim() ?? "";
    }

    private int ParseTotalPages(IDocument document)
    {
        var pageLinks = document.QuerySelectorAll(".pager .pager__item").ToList();
        return pageLinks
            .Select(p => int.TryParse(p.TextContent, out var n) ? n : 0)
            .DefaultIfEmpty(1)
            .Max();
    }
}
