using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Utils;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Ellipse.Services;

public sealed class SchoolService(HttpClient httpClient) : IDisposable
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

            List<SchoolData>? result = await Util.RetryIfInvalid<List<SchoolData>?>(
                r => r != null && r.Count != 0,
                async _ =>
                    await httpClient.GetFromJsonAsync<List<SchoolData>>(
                        $"schools?divisionCode={code}"
                    ),
                maxRetries: 5,
                delayMs: 300
            );

            if (result == null)
                return null;

            return result;
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
