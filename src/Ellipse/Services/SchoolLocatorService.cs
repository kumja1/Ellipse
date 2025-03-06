using System.Net.Http.Json;
using System.Collections.Concurrent;
using Ellipse.Common.Models;

namespace Ellipse.Services;

public sealed class SchoolLocatorService : IDisposable
{
    private readonly GeoService _geoService;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore = new(5);

    private readonly Dictionary<string, int> _divisionCodes = new()
    {
        ["Amelia County"] = 4,
        ["Charles City County"] = 19,
        ["Chesterfield County"] = 21,
        ["Colonial Heights City"] = 106,
        ["Cumberland County"] = 25,
        ["Dinwiddie County"] = 27,
        ["Hanover County"] = 42,
        ["Henrico County"] = 43,
        ["Hopewell City"] = 114,
        ["New Kent County"] = 63,
        ["Petersburg City"] = 120,
        ["Powhatan County"] = 72,
        ["Prince George County"] = 74,
        ["Richmond City"] = 123,
        ["Sussex County"] = 91
    };

    public SchoolLocatorService(GeoService geoService, HttpClient httpClient)
    {
        _geoService = geoService;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
    }

    private async Task<GeoPoint2d> FetchGeoLocation(string name)
    {
        if (string.IsNullOrEmpty(name)) return default;
        return await _geoService.GetLatLngCached(name);
    }

    public async Task<List<SchoolData>> GetSchools()
    {
        Console.WriteLine("[GetSchools] Starting school data collection");

        var tasks = _divisionCodes
            .Select(kvp => ProcessDivision(kvp.Key, kvp.Value))
            .ToList();

        var schools = new List<SchoolData>();
        var queue = new ConcurrentQueue<IAsyncEnumerable<SchoolData>>();

        foreach (var task in tasks)
        {
            queue.Enqueue(await task);
        }

        while (queue.TryDequeue(out var divisionResults))
        {
            await foreach (var school in divisionResults)
            {
                schools.Add(school);
            }
        }

        Console.WriteLine("[GetSchools] Completed.");
        return schools;
    }


    private async Task<IAsyncEnumerable<SchoolData>> ProcessDivision(string name, int code)
    {
        await _semaphore.WaitAsync();
        try
        {
            Console.WriteLine($"[ProcessDivision] Starting {name}");

            var result = await _httpClient.PostAsync("", new FormUrlEncodedContent([
                new KeyValuePair<string, string>("divisionCode", code.ToString())
            ]));

            var schools = await result.Content.ReadFromJsonAsync<List<SchoolData>>() ?? [];

            return FetchGeoLocations(schools);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async IAsyncEnumerable<SchoolData> FetchGeoLocations(List<SchoolData> schools)
    {
        foreach (var school in schools)
        {
            var latLng = await FetchGeoLocation(school.Address);
            yield return school with { LatLng = latLng };
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _semaphore.Dispose();
    }
}
