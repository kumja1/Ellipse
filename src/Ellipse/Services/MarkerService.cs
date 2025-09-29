using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using OpenLayers.Blazor;

namespace Ellipse.Services;

public class MarkerService(HttpClient httpClient)
{
    private const double StepSize = 0.1; // ~11km

    public async IAsyncEnumerable<Marker> GetMarkers(BoundingBox box, List<SchoolData> schools)
    {
        if (schools.Count == 0)
            yield break;

        Console.WriteLine(
            $"[GetMarkers] Generating markers for box ({box.MinLat},{box.MinLng}) to ({box.MaxLat},{box.MaxLng})"
        );

        Console.WriteLine($"[GetMarkers] Total schools: {schools.Count}");
        foreach (GeoPoint2d[] chunk in box.GetPoints(StepSize).Chunk(15))
        {
            IEnumerable<Task<MarkerResponse?>> tasks = chunk.Select(point =>
                GetMarker(point.Lon, point.Lat, schools)
            );

            MarkerResponse?[] responses = await Task.WhenAll(tasks).ConfigureAwait(false);
            for (int i = 0; i < chunk.Length; i++)
            {
                GeoPoint2d point = chunk[i];
                MarkerResponse? response = responses[i];
                if (response == null)
                {
                    Console.WriteLine(
                        $"[GetMarkers] No response for marker at ({point.Lon},{point.Lat})"
                    );
                    continue;
                }

                if (response.Routes == null)
                {
                    Console.WriteLine(
                        $"[GetMarkers] Marker at ({point.Lon},{point.Lat}) has null Routes!"
                    );
                    continue;
                }

                yield return new Marker(MarkerType.MarkerPin, new Coordinate(point.Lon, point.Lat))
                {
                    Properties =
                    {
                        ["Name"] = response.Address,
                        ["Routes"] = response.Routes,
                        ["TotalDistance"] = response.TotalDistance,
                    },
                };
            }
        }
        Console.WriteLine("[GetMarkers] Completed marker generation");
    }

    private async Task<MarkerResponse?> GetMarker(double x, double y, List<SchoolData> schools)
    {
        try
        {
            HttpResponseMessage? response = await CallbackHelper.RetryIfInvalid(
                r => r != null && r.IsSuccessStatusCode,
                async _ =>
                    await httpClient
                        .PostAsJsonAsync("marker", new MarkerRequest(schools, new GeoPoint2d(x, y)))
                        .ConfigureAwait(false)
            );

            MarkerResponse? markerResponse = await response!
                .Content.ReadFromJsonAsync<MarkerResponse>()
                .ConfigureAwait(false);

            return markerResponse!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling server for marker at ({x},{y}): {ex.Message}");
            return null;
        }
    }
}
