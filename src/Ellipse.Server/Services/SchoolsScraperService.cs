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
        var stopwatch = Stopwatch.StartNew();
        Log.Information("[ScrapeDivision] Starting scrape for division {DivisionCode}, OverwriteCache: {OverwriteCache}", 
            divisionCode, overwriteCache);
        
        try
        {
            Log.Debug("[ScrapeDivision] Checking cache for division {DivisionCode}", divisionCode);
            string? cachedData = await cache.GetStringAsync($"division_{divisionCode}");
            
            if (!string.IsNullOrEmpty(cachedData) && !overwriteCache)
            {
                Log.Information("[ScrapeDivision] Cache hit for division {DivisionCode}, returning cached data (size: {Size} bytes)", 
                    divisionCode, cachedData.Length);
                string decompressed = StringHelper.Decompress(cachedData);
                Log.Debug("[ScrapeDivision] Decompressed cache data size: {Size} bytes", decompressed.Length);
                return decompressed;
            }
            
            Log.Information("[ScrapeDivision] Cache miss or overwrite requested for division {DivisionCode}, initiating scrape", 
                divisionCode);

            Log.Debug("[ScrapeDivision] Checking for existing task for division {DivisionCode}", divisionCode);
            bool taskAlreadyExists = _tasks.ContainsKey(divisionCode);
            
            string result = await _tasks
                .GetOrAdd(divisionCode, ScrapeDivisionInternal(divisionCode))
                .ConfigureAwait(false);

            Log.Information("[ScrapeDivision] Task for division {DivisionCode} completed, TaskWasNew: {TaskWasNew}", 
                divisionCode, !taskAlreadyExists);

            if (string.IsNullOrEmpty(result))
            {
                Log.Warning("[ScrapeDivision] Scrape returned empty result for division {DivisionCode}", divisionCode);
                return string.Empty;
            }

            Log.Debug("[ScrapeDivision] Scrape result size: {Size} bytes for division {DivisionCode}", 
                result.Length, divisionCode);
            
            Log.Information("[ScrapeDivision] Compressing and caching result for division {DivisionCode}", divisionCode);
            string compressed = StringHelper.Compress(result);
            Log.Debug("[ScrapeDivision] Compressed size: {Size} bytes (original: {Original} bytes)", 
                compressed.Length, result.Length);
            
            await cache.SetStringAsync($"division_{divisionCode}", compressed);
            Log.Information("[ScrapeDivision] Successfully cached division {DivisionCode}", divisionCode);
            
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
            bool removed = _tasks.TryRemove(divisionCode, out _);
            Log.Debug("[ScrapeDivision] Task cleanup for division {DivisionCode}, Removed: {Removed}", 
                divisionCode, removed);
        }
    }

    private async Task<string> ScrapeDivisionInternal(int divisionCode)
    {
        Log.Information("[ScrapeDivisionInternal] Starting internal scrape for division {DivisionCode}", divisionCode);
        var stopwatch = Stopwatch.StartNew();
        
        List<SchoolData> schools = await ParsePage(divisionCode).ConfigureAwait(false);
        
        stopwatch.Stop();
        Log.Information("[ScrapeDivisionInternal] Scraped {Count} schools from division {DivisionCode} in {ElapsedMs}ms", 
            schools.Count, divisionCode, stopwatch.ElapsedMilliseconds);
        
        Log.Debug("[ScrapeDivisionInternal] Serializing {Count} schools to JSON", schools.Count);
        string json = JsonSerializer.Serialize(schools);
        Log.Debug("[ScrapeDivisionInternal] Serialized JSON size: {Size} bytes", json.Length);
        
        return json;
    }

    private async Task<List<SchoolData>> ParsePage(int divisionCode)
    {
        string url = $"{VIRGINIA_SCHOOLS_URL}?d={divisionCode}";
        Log.Information("[ParsePage] Starting page parse for division {DivisionCode}, URL: {Url}", divisionCode, url);
        
        string divisionName = "";
        int retryAttempt = 0;
        
        List<IElement> rows = await Retry
            .RetryIfListEmpty<IElement>(
                func: async _ =>
                {
                    retryAttempt++;
                    Log.Debug("[ParsePage] Attempt {Attempt} to fetch page for division {DivisionCode}", 
                        retryAttempt, divisionCode);
                    
                    var fetchStopwatch = Stopwatch.StartNew();
                    IDocument document = await _browsingContext.OpenAsync(url).ConfigureAwait(false);
                    fetchStopwatch.Stop();
                    
                    Log.Debug("[ParsePage] Document fetched in {ElapsedMs}ms, Content length: {Length} chars", 
                        fetchStopwatch.ElapsedMilliseconds, document.TextContent.Length);
                    
                    divisionName = document.QuerySelector("tr.division_heading td.division")?.TextContent ??
                                   "";

                    Log.Information("[ParsePage] Division {DivisionCode} name extracted: '{DivisionName}'", 
                        divisionCode, divisionName);
                    
                    var elementList = document
                        .QuerySelectorAll(
                            "table > tbody > tr:not(.tr_header_row, .division_heading, .office_heading, :has(table.public_school_division_division), :has(td.division))")
                        .ToList();
                    
                    Log.Information("[ParsePage] Found {RowCount} school rows for division {DivisionCode}", 
                        elementList.Count, divisionCode);
                    
                    return elementList;
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
        
        Log.Information("[ParsePage] Processing {RowCount} rows for division {DivisionCode} ({DivisionName})", 
            rows.Count, divisionCode, divisionName);
        
        var parseStopwatch = Stopwatch.StartNew();
        SchoolData?[] results =
            await Task.WhenAll(rows.Select((row, index) => 
            {
                Log.Debug("[ParsePage] Parsing row {Index}/{Total} for division {DivisionCode}", 
                    index + 1, rows.Count, divisionCode);
                return ParseRow(row, divisionName);
            })).ConfigureAwait(false);
        parseStopwatch.Stop();
        
        Log.Information("[ParsePage] Parsed all {RowCount} rows in {ElapsedMs}ms for division {DivisionCode}", 
            rows.Count, parseStopwatch.ElapsedMilliseconds, divisionCode);

        int nullCount = results.Count(r => r == null);
        if (nullCount > 0)
        {
            Log.Warning("[ParsePage] {NullCount} out of {Total} rows failed to parse for division {DivisionCode}", 
                nullCount, results.Length, divisionCode);
        }

        var validSchools = results.Where(s =>
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
        Log.Debug("[ParseRow] Starting row parse for division '{DivisionName}'", divisionName);
        
        IElement? infoCell = row.QuerySelector("td.td_column_wrapstyle:first-child ");
        if (infoCell == null)
        {
            Log.Warning("[ParseRow] Info cell not found in row for division '{DivisionName}'", divisionName);
            return null;
        }

        string? name = infoCell.QuerySelector("strong")?.TextContent.Trim();
        Log.Debug("[ParseRow] Extracted school name: '{SchoolName}'", name);
        
        string[] addressSegments = infoCell.ChildNodes
            .OfType<IText>()
            .Select(t => t.TextContent.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t) && t != "Street address:")
            .ToArray();

        Log.Debug("[ParseRow] Found {SegmentCount} address segments for school '{SchoolName}'", 
            addressSegments.Length, name);

        string address = addressSegments.Length > 0
            ? string.Join(", ", addressSegments.Take(addressSegments.Length - 1))
            : "";

        Log.Debug("[ParseRow] Constructed address for '{SchoolName}': '{Address}'", name, address);

        string gradeSpan = WebUtility.HtmlDecode(row.QuerySelector("td:nth-child(2)")?.TextContent ?? "").Trim();
        Log.Information("[ParseRow] School '{SchoolName}' - Division: '{Division}', Grade Span: '{GradeSpan}'", 
            name, divisionName, gradeSpan);
        
        Log.Information("[ParseRow] Fetching coordinates for school '{SchoolName}' at address '{Address}'", 
            name, address);

        Stopwatch geoStopwatch = Stopwatch.StartNew();
        GeoPoint2d latLng = await Retry
            .RetryIfInvalid(
                isValid: c => c != GeoPoint2d.Zero,
                async attempt =>
                {
                    Log.Debug("[ParseRow] Geocoding attempt {Attempt} for school '{SchoolName}'", 
                        attempt, name);
                    return await geoService.GetLatLngCached(address);
                },
                maxRetries: 20,
                delayMs: 500
            )
            .ConfigureAwait(false);
        
        geoStopwatch.Stop();

        if (latLng == GeoPoint2d.Zero)
        {
            Log.Warning("[ParseRow] Failed to geocode address '{Address}' for school '{SchoolName}' in {ElapsedMs}ms", 
                address, name,  geoStopwatch.ElapsedMilliseconds);
        }
        else
        {
            Log.Information("[ParseRow] Successfully geocoded '{SchoolName}' in {ElapsedMs}ms - Address: '{Address}', Coordinates: (Lon: {Lon}, Lat: {Lat})",
                name, geoStopwatch.ElapsedMilliseconds, address, latLng.Lon, latLng.Lat);
        }

        string schoolType = WebUtility.HtmlDecode(row.QuerySelector("td:nth-child(3)")?.TextContent ?? "").Trim();
        Log.Debug("[ParseRow] School type for '{SchoolName}': '{SchoolType}'", name, schoolType);
        
        Log.Information("[ParseRow] Successfully created SchoolData for '{SchoolName}'", name);
        
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
        Log.Information("[Dispose] Disposing SchoolsScraperService, clearing {TaskCount} tasks", _tasks.Count);
        _tasks.Clear();
        Log.Debug("[Dispose] SchoolsScraperService disposed");
    }
}