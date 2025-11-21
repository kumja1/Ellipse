#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using Ellipse.Components.Shared.MapDisplay;
using Ellipse.Components.Shared.Menu;
using Ellipse.Services;
using Ellipse.Common.Utils;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;
using Serilog;
using Ellipse.Common.Models.Directions;

namespace Ellipse.Components.Pages;

partial class MapPage : ComponentBase
{
    private Menu? _menu;
    private MapDisplay? _mapDisplay;

    [Inject]
    public HttpClient Http { get; set; }

    [Inject]
    public SchoolDivisionService? SchoolDivisionService { get; set; }

    private string _selectedRouteName = "Average";
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Log.Information("First render...fetching markers");
            await GetMarkers();
        }
    }

    private async Task GetMarkers()
    {
        List<SchoolData> schools = await SchoolDivisionService!.GetAllSchools();
        BoundingBox box = new(schools.Select(s => s.LatLng));

        TimeSpan? closestMarkerDuration = await FindClosestMarker(box, schools);
        if (_mapDisplay?.Map.MarkersList.Count == 0 || closestMarkerDuration is null)
        {
            Log.Information("No markers found. Returning.");
            return;
        }

        // Do a second pass to highlight alternative markers that are "near" the best duration
        foreach (Marker marker in _mapDisplay?.Map.MarkersList.Cast<Marker>())
        {
            TimeSpan duration = marker.Properties["Routes"]["Average"].Duration;
            bool isNear = (duration - closestMarkerDuration.Value).TotalMinutes <= 30;
            if (!isNear)
                continue;

            Log.Information("Marker {MarkerText} is near the best route.", marker.Text);
            marker.PinColor = PinColor.Blue;
            _menu.AddMarker(marker);
            _mapDisplay.AddOrUpdateMarker(marker);
        }
    }

    private async Task<MarkerResponse?> GetMarker(double x, double y, List<SchoolData> schools)
    {
        try
        {
            HttpResponseMessage? response = await CallbackHelper.RetryIfInvalid(
                r => r is { IsSuccessStatusCode: true },
                async _ =>
                    await Http
                        .PostAsJsonAsync("api/marker", new MarkerRequest(schools, new GeoPoint2d(x, y)))
            );

            if (response == null)
                return null;

            MarkerResponse markerResponse = await response
                .Content.ReadFromJsonAsync<MarkerResponse>();

            return markerResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling server for marker at ({x},{y}): {ex.Message}");
            return null;
        }
    }

    private async Task<TimeSpan?> FindClosestMarker(BoundingBox box, List<SchoolData> schools)
    {
        Marker? closestMarker = null;
        TimeSpan? closestDuration = null;

        foreach (GeoPoint2d[] chunk in box.GetPoints(step: 0.11).Chunk(20))
        {
            MarkerResponse?[] responses = await Task.WhenAll(chunk.Select(point =>
                GetMarker(point.Lon, point.Lat, schools)
            ));

            for (int i = 0; i < responses.Length; i++)
            {
                MarkerResponse? response = responses[i];
                if (response == null)
                {
                    Log.Warning("MarkerResponse is null");
                    continue;
                }

                Marker marker = new(MarkerType.MarkerPin, new Coordinate(chunk[i].Lon, chunk[i].Lat), response?.Address)
                {
                    Properties =
                    {
                        ["Name"] = response!.Address,
                        ["Routes"] = response!.Routes,
                        ["TotalDistance"] = response!.TotalDistance,
                    },
                    TextScale = 0,
                };

                closestMarker ??= marker;
                closestDuration ??= closestMarker.Properties["Routes"]["Average"].Duration;
                TimeSpan newDuration = response.Routes["Average"].Duration;

                if (newDuration >= closestDuration)
                {
                    Log.Information(
                        "Marker {MarkerText} has longer duration than current closest marker {ClosestMarkerText}. Marker Duration: {NewDuration} min, Closest Duration: {ClosestDuration} min.",
                        marker.Text, closestMarker.Text, newDuration, closestDuration);

                    marker.PinColor = PinColor.Red;
                }
                else
                {
                    Log.Information(
                        "Marker {MarkerText} is closer (shorter duration) than previous closest marker {ClosestMarkerText}. Marker Duration: {NewDuration} min, Previous Closest: {ClosestDuration} min.",
                        marker.Text, closestMarker.Text, newDuration, closestDuration);

                    // Update previous closest marker visual
                    closestMarker.PinColor = PinColor.Red;
                    await _mapDisplay.AddOrUpdateMarker(closestMarker);

                    marker.PinColor = PinColor.Green;
                    closestMarker = marker;
                    closestDuration = newDuration;
                }

                _menu?.AddMarker(marker);
                _ = _mapDisplay?.AddOrUpdateMarker(marker);
            }
        }

        return closestDuration;
    }

    private void OnMarkerClicked(Marker marker) => _menu?.SelectMarker(marker);

    Route? GetRouteProperty(Dictionary<string, Route> routes, string propertyName) => routes.TryGetValue(propertyName, out Route value) ? value : default;
}
