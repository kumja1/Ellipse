using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using OpenLayers.Blazor;

namespace Ellipse.Services;

public class SiteFinderService(HttpClient httpClient, SchoolFetcherService schoolLocatorService)
{
    private const double StepSize = 0.1;

    public async IAsyncEnumerable<Marker> GetMarkers()
    {
        var schools = await schoolLocatorService.GetSchools().ConfigureAwait(false);
        if (schools.Count == 0)
            yield break;

        var latLngs = schools.Select(school => school.LatLng).ToList();
        var boundingBox = new BoundingBox(latLngs);

        for (var x = boundingBox.MinLng; x <= boundingBox.MaxLng; x += StepSize)
        {
            for (var y = boundingBox.MinLat; y <= boundingBox.MaxLat; y += StepSize)
            {
                Console.WriteLine($"X:{x}, Y:{y}");
                var marker = await GetMarker(x, y, schools).ConfigureAwait(false);
                if (marker != null)
                    yield return new Marker(
                        MarkerType.MarkerAwesome,
                        new Coordinate(x, y),
                        marker.Address
                    )
                    {
                        Properties = { ["Name"] = marker.Address, ["Routes"] = marker.Routes },
                    };
            }
        }
    }

    private async Task<MarkerResponse?> GetMarker(double x, double y, List<SchoolData> schools)
    {
        try
        {
            var response = await FuncHelper.RetryIfInvalid<HttpResponseMessage>(
                r => r.IsSuccessStatusCode,
                async _ =>
                    await httpClient
                        .PostAsJsonAsync(
                            $"{Settings.ServerUrl}marker/get-markers",
                            new MarkerRequest(schools, new GeoPoint2d(x, y), false)
                        )
                        .ConfigureAwait(false)
            );

            MarkerResponse? markerResponse = await response
                .Content.ReadFromJsonAsync<MarkerResponse>()
                .ConfigureAwait(false);

            return markerResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling server for marker at ({x},{y}): {ex.Message}");
            return null;
        }
    }
}
