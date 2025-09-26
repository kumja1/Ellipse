using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Utils;

namespace Ellipse.Services;

public sealed class SchoolDivisionService(HttpClient httpClient) : IDisposable
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

    // Gets all schools in the specified divisions
    public async Task<List<SchoolData>> GetAllSchools()
    {
        Console.WriteLine("[GeSchools] Starting school data collection");

        var results = await Task.WhenAll(
                _divisionCodes.Select(kvp => GetDivisionSchools(kvp.Key, kvp.Value))
            )
            .ConfigureAwait(false);
        var schools = results.Where(x => x is not null).SelectMany(x => x!);

        Console.WriteLine("[GetSchools] Completed.");
        return [.. schools];
    }

    private async Task<List<SchoolData>?> GetDivisionSchools(string divisionName, int code)
    {
        try
        {
            Console.WriteLine($"[GetDivisionSchools] Starting {divisionName}");

            List<SchoolData>? result = await CallbackHelper.RetryIfInvalid(
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

            Console.WriteLine(
                $"[GetDivisionSchools] Completed {divisionName}. Found {result.Count} schools"
            );

            foreach (var school in result)
            {
                Console.WriteLine(
                    $"{divisionName} ({code}) - {school.Name} ({school.GradeSpan}) {school.LatLng} "
                );
            }

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
