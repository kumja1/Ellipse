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

        for (double y = box.MinLat; y <= box.MaxLat; y += StepSize)
        for (double x = box.MinLng; x <= box.MaxLng; x += StepSize)
        {
            MarkerResponse? response = await GetMarker(x, y, schools).ConfigureAwait(false);
            if (response == null)
                continue;

            yield return new Marker(MarkerType.MarkerPin, new Coordinate(x, y), response.Address)
            {
                Properties =
                {
                    ["Name"] = response.Address,
                    ["Routes"] = response.Routes,
                    ["TotalDistance"] = response.TotalDistance,
                },
                TextScale = 0.7,
                Popup = true,
            };
        }
    }

    private async Task<MarkerResponse?> GetMarker(double x, double y, List<SchoolData> schools)
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
