using System.Text.Json;
using Ellipse.Common.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net;

namespace Ellipse.Services;

public sealed class SchoolLocatorService : IDisposable
{
    private const string BaseUrl = "https://changing-kayley-lum-studios-c585327d.koyeb.app";
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
        _httpClient.Timeout = TimeSpan.FromMinutes(6);
        _httpClient.PostAsync($"{BaseUrl}/api/cors/add-origin", new StringContent(_httpClient.BaseAddress.AbsoluteUri));

    }

    private async Task<GeoPoint2d> FetchGeoLocation(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine($"{name} address is missing. Skipping...");
            return default;
        }
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
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/schools/get-schools");

        request.SetBrowserRequestMode(BrowserRequestMode.NoCors);
        request.Content = new FormUrlEncodedContent([
          new KeyValuePair<string, string>("divisionCode", code.ToString())
      ]);

        var result = await _httpClient.SendAsync(request).ConfigureAwait(false);
        Console.WriteLine(await result.Content.ReadAsStringAsync().ConfigureAwait(false) ?? "[]");
        var schools = JsonSerializer.Deserialize<List<SchoolData>>(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
        var tasks = schools.Select(school => FetchGeoLocation(school.Address)).ToList();

        for (int i = 0; i < tasks.Count; i++)
        {
            schools[i] = schools[i] with { LatLng = await tasks[i] };
        }

        return schools;
    }

    public void Dispose() => _httpClient.Dispose();
}
