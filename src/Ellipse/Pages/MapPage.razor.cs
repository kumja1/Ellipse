#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Components.MapDisplay;
using Ellipse.Components.Menu;
using Ellipse.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using OpenLayers.Blazor;

namespace Ellipse.Pages;

partial class MapPage : ComponentBase
{
    private Menu? _menu;
    private MapDisplay? _mapDisplay;

    [Inject]
    private MarkerService? MarkerService { get; set; }

    [Inject]
    private SchoolDivisionService? SchoolDivisionService { get; set; }

    [Inject]
    private ILogger<MapPage> Logger { get; set; } = default!;

    private string _selectedRouteName = "Average";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Logger.LogInformation("First render, loading markers...");
            await GetMarkers();
        }
    }

    private async Task GetMarkers()
    {
        List<SchoolData> schools = await SchoolDivisionService!.GetAllSchools();

        BoundingBox box = new(schools.Select(s => s.LatLng));
        double? closestMarkerDistance = await FindClosestMarker(box, schools);

        if (_mapDisplay?.Map?.MarkersList.Count == 0 || closestMarkerDistance is null)
            return;

        // Do a second pass to higlight alternative markers
        foreach (var marker in _mapDisplay!.Map.MarkersList.Cast<Marker>())
        {
            double distance = marker.Properties["TotalDistance"];
            bool isNear = Math.Abs(distance - closestMarkerDistance.Value) <= 100;
            if (!isNear)
                continue;

            marker.PinColor = PinColor.Blue;
            await _mapDisplay.AddOrUpdateMarker(marker);
        }
    }

    private async Task<double?> FindClosestMarker(BoundingBox box, List<SchoolData> schools)
    {
        Marker? closestMarker = null;
        double? closestDistance = null;

        await foreach (var markers in MarkerService!.GetMarkers(box, schools))
        {
            if (markers == null)
                continue;

            for (int i = 0; i < markers.Count; i++)
            {
                Marker marker = markers[i];

                closestMarker ??= marker;
                closestDistance ??= closestMarker.Properties["TotalDistance"];
                double newDistance = marker.Properties["TotalDistance"];

                if (newDistance >= closestDistance)
                {
                    Logger.LogInformation(
                        "Marker {MarkerText} is farther than closest marker {ClosestMarkerText}. Marker Distance: {NewDistance}, Closest Distance: {ClosestDistance}.",
                        marker.Text, closestMarker.Text, newDistance, closestDistance);

                    marker.PinColor = PinColor.Red;
                }
                else
                {
                    Logger.LogInformation(
                        "Marker {MarkerText} is closer than previous closest marker {ClosestMarkerText}. Marker Distance: {NewDistance}, Previous Closest: {ClosestDistance}.",
                        marker.Text, closestMarker.Text, newDistance, closestDistance);

                    // Update previous closest marker
                    closestMarker.PinColor = PinColor.Red;
                    await _mapDisplay.AddOrUpdateMarker(closestMarker);

                    marker.PinColor = PinColor.Green;
                    closestMarker = marker;
                    closestDistance = newDistance;
                }

                await _mapDisplay.AddOrUpdateMarker(marker);
            }


        }

        return closestDistance;
    }

    public void SelectMarker(Marker marker) => _menu!.SelectMarker(marker);
}
