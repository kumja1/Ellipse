using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Ellipse.Common.Models;
using Ellipse.Common.Utils;
using Ellipse.Server.Services;
using Serilog;

namespace Ellipse.Server.Utils;

public sealed partial class WebScraper(
    int divisionCode,
    GeocodingService geoService,
    IBrowsingContext context
)
{
    private const string VirginiaSchoolsUrl = "https://schoolquality.virginia.gov/schools";

    [GeneratedRegex(@"[.\s/]+")]
    private static partial Regex SchoolNameRegex();

    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public async Task<string> Scrape()
    {
        var (schools, totalPages) = await ParsePage(1).ConfigureAwait(false);
        if (totalPages > 1)
        {
            await Task.WhenAll(
                Enumerable
                    .Range(2, totalPages - 1)
                    .Select(
                        async (page, _) =>
                        {
                            var (pageSchools, _) = await ParsePage(page).ConfigureAwait(false);
                            schools.AddRange(pageSchools);
                        }
                    )
            );
        }

        Log.Information("Scraped {Count} schools from {Division}", schools.Count, divisionCode);
        return JsonSerializer.Serialize(schools);
    }

    private async Task<(List<SchoolData> Schools, int TotalPages)> ParsePage(int page)
    {
        string url = $"{VirginiaSchoolsUrl}/page/{page}?division={divisionCode}";
        IDocument? document = null;
        List<IElement>? rows = await CallbackHelper
            .RetryIfInvalid(
                isValid: l => l?.Count > 0,
                func: async _ =>
                {
                    document = await context.OpenAsync(url).ConfigureAwait(false);
                    return document.QuerySelectorAll("table > tbody > tr").ToList();
                },
                defaultValue: [],
                maxRetries: 10,
                delayMs: 300
            )
            .ConfigureAwait(false);

        ArgumentNullException.ThrowIfNull(rows, nameof(rows));
        var results = await Task.WhenAll(rows.Select(ParseRow)).ConfigureAwait(false);

        List<SchoolData> schools = [.. results.Where(s => s != null).Cast<SchoolData>()];
        int totalPages = ParseTotalPages(document!);
        return (schools, totalPages);
    }

    private async Task<SchoolData?> ParseRow(IElement row)
    {
        IElement? nameCell = row.QuerySelector("td:first-child");
        if (nameCell == null)
            return null;

        string name = WebUtility.HtmlDecode(nameCell.TextContent).Trim();
        string schoolName = SchoolNameRegex().Replace(name.ToLower(), "-");

        string division = row.QuerySelector("td:nth-child(2)")?.TextContent.Trim() ?? "";
        string gradeSpan = row.QuerySelector("td:nth-child(3)")?.TextContent.Trim() ?? "";
        Log.Information("{School} Division: {Division}", name, division);
        Log.Information("{School} Grade Span: {GradeSpan}", name, gradeSpan);

        string? address = await FetchAddressAsync(schoolName).ConfigureAwait(false);
        if (address == null)
        {
            Log.Warning("Address not found for school: {School}", name);
            return null;
        }

        Log.Information("Fetching coordinates for school: {School}", name);
        GeoPoint2d latLng = await CallbackHelper
            .RetryIfInvalid(
                isValid: c => c != GeoPoint2d.Zero,
                async _ => await geoService.GetLatLngCached(address),
                maxRetries: 20,
                delayMs: 500
            )
            .ConfigureAwait(false);

        Log.Information(
            "{School} Address: {Address}, Lon: {Lon}, Lat: {Lat}",
            name,
            address,
            latLng.Lon,
            latLng.Lat
        );

        return new SchoolData
        {
            Name = name,
            Address = address,
            Division = division,
            GradeSpan = gradeSpan,
            LatLng = latLng,
        };
    }

    private async Task<string?> FetchAddressAsync(string schoolName)
    {
        try
        {
            string? address = await CallbackHelper
                .RetryIfInvalid(
                    isValid: s => !string.IsNullOrEmpty(s),
                    func: async attempt =>
                    {
                        try
                        {
                            Log.Information(
                                "Attempt {Attempt}: Fetching address for {Name}",
                                attempt,
                                schoolName
                            );

                            await _semaphore.WaitAsync().ConfigureAwait(false);

                            string url = $"{VirginiaSchoolsUrl}/{schoolName}";
                            Log.Information("Requesting: {Url}", url);

                            IDocument doc = await context.OpenAsync(url).ConfigureAwait(false);
                            IElement? el = doc.QuerySelector(
                                "[itemtype='http://schema.org/PostalAddress']"
                            );

                            return WebUtility.HtmlDecode(el?.TextContent ?? "").Trim();
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    },
                    maxRetries: 20
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

    private static int ParseTotalPages(IDocument document) =>
        document
            .QuerySelectorAll("a.page-numbers")
            .Select(e => int.TryParse(e.TextContent, out int p) ? p : 0)
            .Append(1)
            .Max();
}
