using System.Net.Http.Json;
using Ellipse.Common.Models;

namespace Ellipse.Services;

public sealed class SchoolLocatorService : IDisposable
{
    private readonly GeoService _geoService;
    private readonly HttpClient _httpClient;

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

        var schools = (await Task.WhenAll(tasks).ConfigureAwait(false)).SelectMany(e => e);

        Console.WriteLine("[GetSchools] Completed.");
        return schools.ToList();
    }


    private async Task<List<SchoolData>> ProcessDivision(string name, int code)
    {

        Console.WriteLine($"[ProcessDivision] Starting {name}");

        var result = await _httpClient.PostAsync("https://changing-kayley-lum-studios-c585327d.koyeb.app/api/schools/get-schools", new FormUrlEncodedContent([
            new KeyValuePair<string, string>("divisionCode", code.ToString())
        ]));

        var schools = await result.Content.ReadFromJsonAsync<List<SchoolData>>() ?? [];
        var tasks = schools.Select(school => FetchGeoLocation(school.Address)).ToList();

        for (int i = 0; i < tasks.Count; i++)
        {
            schools[i] = schools[i] with { LatLng = await tasks[i] };
        }

        return schools;
    }

    public void Dispose() => _httpClient.Dispose();
}
