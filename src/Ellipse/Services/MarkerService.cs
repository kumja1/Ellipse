using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using OpenLayers.Blazor;

namespace Ellipse.Services;

public class MarkerService(HttpClient httpClient)
{
    private const double StepSize = 0.1;

    public async IAsyncEnumerable<Marker> GetMarkers(BoundingBox box, List<SchoolData> schools)
    {
        if (schools.Count == 0)
            yield break;

        for (double x = box.MinLng; x <= box.MaxLng; x += StepSize)
        for (double y = box.MinLat; y <= box.MaxLat; y += StepSize)
        {
            MarkerResponse? marker = await RequestMarker(x, y, schools).ConfigureAwait(false);
            if (marker == null)
                continue;

            yield return new Marker(MarkerType.MarkerAwesome, new Coordinate(x, y), marker.Address)
            {
                Properties =
                {
                    ["Name"] = marker.Address,
                    ["Routes"] = marker.Routes,
                    ["TotalDistance"] = marker.TotalDistance,
                },
            };
        }
    }

    private async Task<MarkerResponse> RequestMarker(double x, double y, List<SchoolData> schools)
    {
        try
        {
            HttpResponseMessage? response = await CallbackHelper.RetryIfInvalid(
                r => r != null && r.IsSuccessStatusCode,
                async _ =>
                    await httpClient
                        .PostAsJsonAsync(
                            "marker",
                            new MarkerRequest(schools, new GeoPoint2d(x, y), false)
                        )
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
