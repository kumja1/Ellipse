using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Utils;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Ellipse.Services;

public sealed class SchoolFetcherService(HttpClient httpClient) : IDisposable
{
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

    public async Task<List<SchoolData>> GetSchools()
    {
        Console.WriteLine("[GetSchools] Starting school data collection");

        var tasks = _divisionCodes.Select(kvp => ProcessDivision(kvp.Key, kvp.Value)).ToList();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var schools = results.Where(x => x is not null).SelectMany(x => x);

        Console.WriteLine("[GetSchools] Completed.");
        return [.. schools];
    }

    private async Task<List<SchoolData>?> ProcessDivision(string name, int code)
    {
        try
        {
            Console.WriteLine($"[ProcessDivision] Starting {name}");

            HttpResponseMessage? result = await FuncHelper.RetryIfInvalid<HttpResponseMessage>(
                r => r is { IsSuccessStatusCode: true },
                async _ =>
                {
                    HttpRequestMessage request = new(
                        HttpMethod.Post,
                        $"{Settings.ServerUrl}schools/get-schools"
                    );

                    request.SetBrowserRequestMode(BrowserRequestMode.Cors);
                    request.Content = new FormUrlEncodedContent(
                        [new KeyValuePair<string, string>("divisionCode", code.ToString())]
                    );
                    return await httpClient.SendAsync(request).ConfigureAwait(false);
                },
                maxRetries: 5,
                delayMs: 300
            );
            
            if (result == null)
                return null;
            
            var schools = await result
                .Content.ReadFromJsonAsync<List<SchoolData>>()
                .ConfigureAwait(false);

            return schools;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ProcessDivision] An error occured while processing division {code}: {ex.Message}, {ex.StackTrace}"
            );
            return [];
        }
    }

    public void Dispose() => httpClient.Dispose();
}
