using System.Net.Http.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using OpenLayers.Blazor;

namespace Ellipse.Services;

public class SiteFinderService(HttpClient httpClient, SchoolFetcherService schoolLocatorService)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly SchoolFetcherService _schoolLocatorService = schoolLocatorService;
    private const double STEP_SIZE = 0.1;

    public async IAsyncEnumerable<Marker> GetMarkers()
    {
        var schools = await _schoolLocatorService.GetSchools().ConfigureAwait(false);
        if (schools.Count == 0)
            yield break;

        var latLngs = schools.Select(school => school.LatLng).ToList();
        var boundingBox = new BoundingBox(latLngs);

        foreach (var (x, y) in GenerateGrid(boundingBox))
        {
            Console.WriteLine($"X:{x}, Y:${y}");
            var marker = await GetMarker(x, y, schools).ConfigureAwait(false);
            if (marker != null)
                yield return new Marker(
                    MarkerType.MarkerAwesome,
                    new Coordinate(x, y),
                    marker.Address,
                    PinColor.Red
                )
                {
                    Properties = { ["Name"] = marker.Address, ["Routes"] = marker.Routes },
                };
        }
    }

    private async Task<MarkerResponse?> GetMarker(double x, double y, List<SchoolData> schools)
    {
        try
        {
            var response = await _httpClient
                .PostAsJsonAsync(
                    $"{Settings.ServerUrl}marker/get-markers",
                    new MarkerRequest(schools, new GeoPoint2d(x, y))
                )
                .ConfigureAwait(false);
            var markerResponse = await response
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

    private static IEnumerable<(double x, double y)> GenerateGrid(BoundingBox boundingBox)
    {
        for (var x = boundingBox.MinLat; x <= boundingBox.MaxLat; x += STEP_SIZE)
        for (var y = boundingBox.MinLng; y <= boundingBox.MaxLng; y += STEP_SIZE)
            yield return (x, y);
    }
}
