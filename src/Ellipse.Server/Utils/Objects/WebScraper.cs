using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Ellipse.Common.Models;
using Ellipse.Common.Utils;
using Ellipse.Server.Services;
using Serilog;

namespace Ellipse.Server.Utils.Objects;

public sealed partial class WebScraper(int divisionCode, GeoService geoService)
{
    private const string BaseUrl = "https://schoolquality.virginia.gov/virginia-schools";
    private const string SchoolInfoUrl = "https://schoolquality.virginia.gov/schools";

    [GeneratedRegex(@"[.\s/]+")]
    private static partial Regex CleanNameRegex();

    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(
        Configuration.Default.WithDefaultLoader().WithXPath()
    );

    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public async Task<string> ScrapeAsync()
    {
        var sw = Stopwatch.StartNew();
        var (firstPageSchools, totalPages) = await ProcessPageAsync(1).ConfigureAwait(false);
        var queue = new ConcurrentQueue<SchoolData>(firstPageSchools);

        if (totalPages > 1)
        {
            await Task.WhenAll(
                Enumerable
                    .Range(2, totalPages - 1)
                    .Select(
                        async (page, _) =>
                        {
                            var (schools, _) = await ProcessPageAsync(page).ConfigureAwait(false);
                            foreach (var school in schools)
                                queue.Enqueue(school);
                        }
                    )
            );
        }

        sw.Stop();
        return JsonSerializer.Serialize(queue.ToList());
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ProcessPageAsync(int page)
    {
        var url = $"{BaseUrl}/page/{page}?division={divisionCode}";
        IDocument? document = null;
        List<IElement> rows = await FuncHelper
            .RetryIfInvalid(
                isValid: l => l.Count > 0,
                func: async _ =>
                {
                    document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
                    return document.QuerySelectorAll("table > tbody > tr").ToList();
                },
                defaultValue: [],
                maxRetries: 10,
                delayMs: 300
            )
            .ConfigureAwait(false);

        var tasks = rows.Select(ProcessRowAsync);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var schools = results.Where(s => s != null).Cast<SchoolData>().ToList();
        int totalPages = ParseTotalPages(document!);
        return (schools, totalPages);
    }

    private async Task<SchoolData?> ProcessRowAsync(IElement row)
    {
        var nameCell = row.QuerySelector("td:first-child");
        if (nameCell == null)
            return null;

        var name = WebUtility.HtmlDecode(nameCell.TextContent).Trim();
        var cleanedName = CleanNameRegex().Replace(name.ToLower(), "-");

        var cell2 = row.QuerySelector("td:nth-child(2)")?.TextContent.Trim() ?? "";
        var cell3 = row.QuerySelector("td:nth-child(3)")?.TextContent.Trim() ?? "";

        var address = await FetchAddressAsync(cleanedName).ConfigureAwait(false);
        var geoLocation = await FuncHelper
            .RetryIfInvalid(
                isValid: c => c != GeoPoint2d.Zero,
                func: async _ => await geoService.GetLatLngCached(address),
                defaultValue: GeoPoint2d.Zero
            )
            .ConfigureAwait(false);

        return new SchoolData
        {
            Name = name,
            Address = address,
            Division = cell2,
            GradeSpan = cell3,
            LatLng = geoLocation,
        };
    }

    private async Task<string> FetchAddressAsync(string cleanedName)
    {
        try
        {
            string address = await FuncHelper
                .RetryIfInvalid(
                    isValid: s => !string.IsNullOrEmpty(s),
                    func: async (attempt) =>
                    {
                        try
                        {
                            Log.Information(
                                "Attempt {Attempt}: Fetching address for {Name}",
                                attempt,
                                cleanedName
                            );

                            string fetchedAddress = string.Empty;
                            await _semaphore.WaitAsync().ConfigureAwait(false);
                            try
                            {
                                var url = $"{SchoolInfoUrl}/{cleanedName}";
                                Log.Information("Requesting: {Url}", url);

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
                            Log.Warning(ex, "Error fetching from page");
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
            Log.Error(ex, "Fatal error during address fetch");
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
        return totalPages;
    }
}
