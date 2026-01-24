using System.Collections.Concurrent;
using System.Diagnostics;
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
        Stopwatch stopwatch = Stopwatch.StartNew();
        Log.Information(
            "[ScrapeDivision] Starting scrape for division {DivisionCode}, OverwriteCache: {OverwriteCache}",
            divisionCode, overwriteCache);

        try
        {
            geoService.EnableCacheOverwrite(overwriteCache);

            string cacheKey = CacheHelper.CreateCacheKey(nameof(divisionCode), divisionCode);
            if (!overwriteCache)
            {
                string? cachedData = await cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    Log.Information(
                        "[ScrapeDivision] Cache hit for division {DivisionCode}",
                        divisionCode);
                    string decompressed = CacheHelper.DecompressData(cachedData);
                    return decompressed;
                }
            }

            string result = await _tasks
                .GetOrAdd(divisionCode, ScrapeDivisionInternal(divisionCode))
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(result))
            {
                Log.Warning("[ScrapeDivision] Scrape returned empty result for division {DivisionCode}", divisionCode);
                return string.Empty;
            }

            string compressed = CacheHelper.CompressData(result);
            await cache.SetStringAsync(cacheKey, compressed);

            stopwatch.Stop();
            Log.Information("[ScrapeDivision] Completed scrape for division {DivisionCode} in {ElapsedMs}ms",
                divisionCode, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[ScrapeDivision] Error scraping division {DivisionCode} after {ElapsedMs}ms",
                divisionCode, stopwatch.ElapsedMilliseconds);
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

        Log.Information(
            "[ScrapeDivisionInternal] Scraped {Count} schools from division {DivisionCode}",
            schools.Count, divisionCode);

        return JsonSerializer.Serialize(schools);
    }

    private async Task<List<SchoolData>> ParsePage(int divisionCode)
    {
        string url = $"{VIRGINIA_SCHOOLS_URL}?d={divisionCode}&w=true";

        string divisionName = "";
        List<IElement> rows = await Retry
            .RetryIfListEmpty<IElement>(
                func: async _ =>
                {
                    Url? requestUrl = Url.Create(url);
                    if (requestUrl is null)
                    {
                        throw new InvalidOperationException(
                            $"[ParsePage] Failed to create a valid URL from '{url}' for division {divisionCode}."
                        );
                    }

                    IDocument document = await _browsingContext.OpenAsync(requestUrl).ConfigureAwait(false);
                    if (document is null or
                        {
                            Body: null
                        })
                    {
                        throw new InvalidOperationException(
                            $"[ParsePage] AngleSharp returned null document for URL '{url}' (division {divisionCode})."
                        );
                    }

                    divisionName = document.QuerySelector("tr.division_heading td.division")?.TextContent ??
                                   "";

                    return document
                        .QuerySelectorAll(
                            "table > tbody > tr:not(.tr_header_row, .division_heading, .office_heading, :has(table.public_school_division_division:has(tr.public_school_division_division_tr)), :has(td.division))")
                        .Where(r => r.QuerySelector("td.td_column_wrapstyle:has(> strong)") != null)
                        .ToList();
                },
                maxRetries: 10,
                delayMs: 300
            )
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(divisionName))
        {
            Log.Warning("[ParsePage] Division name is empty for division code {DivisionCode}!", divisionCode);
        }

        ArgumentNullException.ThrowIfNull(rows);

        SchoolData?[] results =
            await Task.WhenAll(rows.Select(row => ParseRow(row, divisionName))).ConfigureAwait(false);

        int nullCount = results.Count(r => r == null);
        if (nullCount > 0)
        {
            Log.Warning("[ParsePage] {NullCount} out of {Total} rows failed to parse for division {DivisionCode}",
                nullCount, results.Length, divisionCode);
        }

        List<SchoolData> validSchools = results.Where(s =>
        {
            if (s != null) return true;

            Log.Warning("[ParsePage] Failed to parse school in division {DivisionCode}", divisionCode);
            return false;
        }).Cast<SchoolData>().ToList();

        Log.Information("[ParsePage] Completed parsing division {DivisionCode} with {ValidCount} valid schools",
            divisionCode, validSchools.Count);

        return validSchools;
    }

    private async Task<SchoolData?> ParseRow(IElement row, string divisionName)
    {
        IElement? infoCell = row.QuerySelector("td.td_column_wrapstyle:has(> strong)");
        if (infoCell == null)
        {
            Log.Warning("[ParseRow] Info cell not found in row for division '{DivisionName}'. Row: {RowHtml}",
                divisionName, row.Html());
            return null;
        }

        string? name = infoCell.QuerySelector("strong")?.TextContent.Trim();
        string phoneNumber = infoCell.TextContent.Trim();

        string[] addressSegments = infoCell.ChildNodes
            .OfType<IText>()
            .Select(t => t.TextContent.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t) && t != "Street address:")
            .ToArray();

        string address = addressSegments.Length > 0
            ? string.Join(", ", addressSegments.Take(addressSegments.Length - 1))
            : "";

        Task<GeoPoint2d> task = Retry
            .RetryIfInvalid(
                isValid: c => c != GeoPoint2d.Zero,
                async _ => await geoService.GetLatLngCached(address),
                maxRetries: 20,
                delayMs: 500
            );

        string principal = WebUtility.HtmlDecode(row.QuerySelector("td:nth-child(2)")?.TextContent ?? "").Trim();
        string gradeSpan = WebUtility.HtmlDecode(row.QuerySelector("td:nth-child(3)")?.TextContent ?? "").Trim();
        string schoolType = WebUtility.HtmlDecode(row.QuerySelector("td:nth-child(4)")?.TextContent ?? "").Trim();

        GeoPoint2d latLng = await task;
        if (latLng == GeoPoint2d.Zero)
            Log.Warning("[ParseRow] Failed to geocode address '{Address}' for school '{SchoolName}'",
                address, name);

        return new SchoolData
        {
            Name = name,
            PhoneNumber = phoneNumber,
            PrincipalName = principal,
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