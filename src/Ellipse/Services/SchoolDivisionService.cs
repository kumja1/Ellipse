using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Utils;
using Serilog;

namespace Ellipse.Services;

public sealed class SchoolDivisionService(HttpClient httpClient)
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

    public async Task<SchoolData[]> GetAllSchools()
    {
        Log.Information("Starting school data collection");
        List<SchoolData>?[] results = await Task.WhenAll(
            _divisionCodes.Select(kvp => GetDivisionSchools(kvp.Key, kvp.Value))
        );

        SchoolData[] schools = [.. results.Where(x => x is not null).SelectMany(x => x!)];
        Log.Information("Completed school fetch. Current Count: {Length}. Removing duplicates…",
            schools.Length);

        return [.. schools.DistinctBy(s => s.LatLng)];
    }

    private async Task<List<SchoolData>?> GetDivisionSchools(string divisionName, int code)
    {
        try
        {
            Log.Information("Fetching schools for division {Division}", divisionName);

            List<SchoolData> result = await Retry.RetryIfListEmpty(
                async _ =>
                    (await httpClient.GetFromJsonAsync<List<SchoolData>>(
                        $"api/schools?divisionCode={code}"
                    ))!,
                maxRetries: 30,
                delayMs: 500
            );

            if (result.Count == 0)
            {
                Log.Warning("Failed to retrieve schools for {Division} ({Code})", divisionName, code);
                return null;
            }

            Log.Information("Completed fetching {Division}. Found {Count} schools",
                divisionName, result.Count);

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "An error occurred while retrieving schools for division {Code}", code);
            return [];
        }
    }
}