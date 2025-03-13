// File: Services/MarkerClientService.cs
using System.Net.Http.Json;
using OpenLayers.Blazor;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;

namespace Ellipse.Services;

public class SiteFinderService
{
    private readonly HttpClient _httpClient;
    private readonly SchoolLocatorService _schoolLocatorService;
    private const double STEP_SIZE = 0.1;

    public SiteFinderService(HttpClient httpClient, SchoolLocatorService schoolLocatorService)
    {
        _httpClient = httpClient;
        _schoolLocatorService = schoolLocatorService;
    }

    public async IAsyncEnumerable<Marker> GetMarkers()
    {

        var schools = await _schoolLocatorService.GetSchools().ConfigureAwait(false);
        if (schools.Count == 0)
            yield break;

        var latLngs = schools.Select(school => school.LatLng).ToList();
        var boundingBox = new BoundingBox(latLngs);
        var gridPoints = GenerateGrid(boundingBox).ToList();

        foreach (var (x, y) in gridPoints)
        {
            Console.WriteLine($"X:{x}, Y:${y}");
            var marker = await GetMarkerFromServer(x, y, schools).ConfigureAwait(false);
            if (marker != null)
                yield return new Marker(MarkerType.MarkerAwesome, new Coordinate(x, y), marker.Address, PinColor.Red)
                {
                    Properties = {
                      ["Name"] = marker.Address,
                      ["Distances"] = marker.Distances,
                    }
                };
        }
    }

    private async Task<MarkerResponse?> GetMarkerFromServer(double x, double y, List<SchoolData> schools)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Settings.ServerUrl}marker/get-markers", new MarkerRequest(schools,new GeoPoint2d(x,y))).ConfigureAwait(false);
            var markerResponse = await response.Content.ReadFromJsonAsync<MarkerResponse>().ConfigureAwait(false);

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


public record BoundingBox
{
    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLng { get; }
    public double MaxLng { get; }

    public BoundingBox(List<GeoPoint2d> latLngs)
    {
        if (latLngs.Count == 0)
            throw new ArgumentException("LatLngs list cannot be empty", nameof(latLngs));

        MinLat = latLngs.Min(latLng => latLng.Lat);
        MaxLat = latLngs.Max(latLng => latLng.Lat);
        MinLng = latLngs.Min(latLng => latLng.Lon);
        MaxLng = latLngs.Max(latLng => latLng.Lon);
    }
}
