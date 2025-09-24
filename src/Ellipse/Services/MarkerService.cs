using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using OpenLayers.Blazor;

namespace Ellipse.Services;

public class MarkerService(HttpClient httpClient, SchoolDivisionService schoolService)
{
    private const double StepSize = 0.1;

    public async Task<List<Marker?>> GetMarkers()
    {
        var schools = await schoolService.GetAllSchools().ConfigureAwait(false);
        if (schools.Count == 0)
            return [];

        var latLngs = schools.Select(school => school.LatLng).ToList();
        BoundingBox boundingBox = new(latLngs);
        Console.WriteLine($"Bounding box calculation:");
        Console.WriteLine($"Min Lat: {boundingBox.MinLat}");
        Console.WriteLine($"Max Lat: {boundingBox.MaxLat}");
        Console.WriteLine($"Min Lng: {boundingBox.MinLng}");
        Console.WriteLine($"Max Lng: {boundingBox.MaxLng}");

        return
        [
            .. await Task.WhenAll(
                    GenerateGrid(boundingBox, StepSize)
                        .Select(async coord =>
                        {
                            MarkerResponse? marker = await RequestMarker(coord.X, coord.Y, schools)
                                .ConfigureAwait(false);
                            if (marker == null)
                                return null;

                            return new Marker(
                                MarkerType.MarkerAwesome,
                                new Coordinate(coord.X, coord.Y),
                                marker.Address
                            )
                            {
                                Properties =
                                {
                                    ["Name"] = marker.Address,
                                    ["Routes"] = marker.Routes,
                                },
                            };
                        })
                )
                .ConfigureAwait(false),
        ];
    }

    private async Task<MarkerResponse?> RequestMarker(double x, double y, List<SchoolData> schools)
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

    private IEnumerable<(double X, double Y)> GenerateGrid(BoundingBox box, double step)
    {
        for (double y = box.MinLat; y <= box.MaxLat; y += step)
        for (double x = box.MinLng; x <= box.MaxLng; x += step)
            yield return (x, y);
    }
}
