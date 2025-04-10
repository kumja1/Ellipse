using System.Net.Http.Json;
using Ellipse.Common.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Ellipse.Services;

public sealed class SchoolFetcherService : IDisposable
{
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
        ["Sussex County"] = 91,
    };

    public SchoolFetcherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(10);

        // _httpClient.PostAsync($"{BaseUrl}cors/add-origin", new StringContent(@$"{{""origin"": ""{_httpClient.BaseAddress.AbsoluteUri}""}}", Encoding.UTF8, "application/json"));
    }

    public async Task<List<SchoolData>> GetSchools()
    {
        Console.WriteLine("[GetSchools] Starting school data collection");

        var tasks = _divisionCodes.Select(kvp => ProcessDivision(kvp.Key, kvp.Value)).ToList();

        var schools = (await Task.WhenAll(tasks).ConfigureAwait(false)).SelectMany(e => e);

        Console.WriteLine("[GetSchools] Completed.");
        return [.. schools];
    }

    private async Task<List<SchoolData>> ProcessDivision(string name, int code)
    {
        Console.WriteLine($"[ProcessDivision] Starting {name}");
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{Settings.ServerUrl}schools/get-schools"
        );

        request.SetBrowserRequestMode(BrowserRequestMode.Cors);
        request.Content = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("divisionCode", code.ToString())]
        );

        var result = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var schools = await result
            .Content.ReadFromJsonAsync<List<SchoolData>>()
            .ConfigureAwait(false);

        return schools;
    }

    public void Dispose() => _httpClient.Dispose();
}
