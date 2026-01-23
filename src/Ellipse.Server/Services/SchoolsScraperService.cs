using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using Ellipse.Common.Models;
using Ellipse.Common.Utils;
using Ellipse.Server.Utils;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;

namespace Ellipse.Server.Services;

public sealed class SchoolsScraperService(GeocodingService geoService, IDistributedCache cache)
    : IDisposable
{
    private readonly ConcurrentDictionary<int, Task<string>> _tasks = new();

    private const string VIRGINIA_SCHOOLS_URL = "https://www.va-doeapp.com/PublicSchoolsByDivisions.aspx";

    // private readonly SemaphoreSlim _semaphore = new(20, 20);

    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(
        Configuration.Default.WithDefaultLoader().WithXPath()
    );

    public async ValueTask<string> ScrapeDivision(int divisionCode, bool overwriteCache = false)
    {
        try
        {
            string? cachedData = await cache.GetStringAsync($"division_{divisionCode}");
            if (!string.IsNullOrEmpty(cachedData) && !overwriteCache)
                return StringHelper.Decompress(cachedData);

            string result = await _tasks
                .GetOrAdd(divisionCode, ScrapeDivisionInternal(divisionCode))
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(result))
                return string.Empty;

            await cache.SetStringAsync($"division_{divisionCode}", StringHelper.Compress(result));
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ScrapeDivision] Error scraping division: {DivisionCode}", divisionCode);
            return string.Empty;
        }
        finally
        {
            _tasks.TryRemove(divisionCode, out _);
        }
    }

    private async Task<string> ScrapeDivisionInternal(int divisionCode)
    {
        List<SchoolData> schools = await ParsePage(divisionCode).ConfigureAwait(false);
        Log.Information("Scraped {Count} schools from {Division}", schools.Count, divisionCode);
        return JsonSerializer.Serialize(schools);
    }

    private async Task<List<SchoolData>> ParsePage(int divisionCode)
    {
        string url = $"{VIRGINIA_SCHOOLS_URL}?d={divisionCode}";
        string divisionName = "";
        List<IElement> rows = await Retry
            .RetryIfListEmpty<IElement>(
                func: async _ =>
                {
                    IDocument document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
                    Log.Information("Document: {Document}", document.TextContent);
                    divisionName = document.QuerySelector("tr.division_heading td.division")?.TextContent ??
                                   "";

                    Log.Information("Division Name: {DivisionName}", divisionName);
                    return document
                        .QuerySelectorAll(
                            "table > tbody > tr:not(.tr_header_row, .division_heading, .office_heading, :has(table.public_school_division_division), :has(td.division))")
                        .ToList();
                },
                maxRetries: 10,
                delayMs: 300
            )
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(divisionName))
            Log.Warning("Name of {DivisionCode} is empty!", divisionCode);

        ArgumentNullException.ThrowIfNull(rows);
        SchoolData?[] results =
            await Task.WhenAll(rows.Select(row => ParseRow(row, divisionName))).ConfigureAwait(false);

        return
        [
            ..results.Where(s =>
            {
                if (s != null) return true;

                Log.Warning("Failed to parse school in division {Division}: {School}", divisionCode, s);
                return false;
            }).Cast<SchoolData>()
        ];
    }

    private async Task<SchoolData?> ParseRow(IElement row, string divisionName)
    {
        IElement? infoCell = row.QuerySelector("td.td_column_wrapstyle:first-child ");
        if (infoCell == null)
            return null;

        string? name = infoCell.QuerySelector("strong")?.TextContent.Trim();
        string[] addressSegments = infoCell.ChildNodes
            .OfType<IText>()
            .Select(t => t.TextContent.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t) && t != "Street address:")
            .ToArray();

        string address = addressSegments.Length > 0
            ? string.Join(", ", addressSegments.Take(addressSegments.Length - 1))
            : "";

        string gradeSpan = WebUtility.HtmlDecode(row.QuerySelector("td:nth-child(2)")?.TextContent ?? "").Trim();
        Log.Information("{School} Division: {Division}", name, divisionName);
        Log.Information("{School} Grade Span: {GradeSpan}", name, gradeSpan);
        Log.Information("Fetching coordinates for school: {School}", name);

        GeoPoint2d latLng = await Retry
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

        string schoolType = WebUtility.HtmlDecode(row.QuerySelector("td:nth-child(3)")?.TextContent ?? "").Trim();
        return new SchoolData
        {
            Name = name,
            Address = address,
            Division = divisionName,
            GradeSpan = gradeSpan,
            SchoolType = schoolType,
            LatLng = latLng,
        };
    }

    public void Dispose()
    {
        _tasks.Clear();
    }
}