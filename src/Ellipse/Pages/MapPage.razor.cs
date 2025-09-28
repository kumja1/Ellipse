#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Components.MapDisplay;
using Ellipse.Components.Menu;
using Ellipse.Services;
using Microsoft.AspNetCore.Components;
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
    private NavigationManager? NavigationManager { get; set; }

    private string _selectedRouteName = "Average";

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await GetMarkers();
    }

    private async Task GetMarkers()
    {
        List<SchoolData> schools = await SchoolDivisionService!
            .GetAllSchools()
            .ConfigureAwait(false);

        BoundingBox box = new(schools.Select(s => s.LatLng));
        double closestMarkerDistance = await FindClosestMarker(box, schools);

        foreach (Marker marker in _mapDisplay!.Map.MarkersList.Cast<Marker>()) // Do a second pass to color nearby markers
        {
            double distance = marker.Properties["TotalDistance"];
            bool isNear = Math.Abs(distance - closestMarkerDistance) <= 100;
            if (!isNear)
                continue;

            marker.PinColor = PinColor.Blue;
            await _mapDisplay!.AddOrUpdateMarker(marker);
        }
    }

    private async Task<double> FindClosestMarker(BoundingBox box, List<SchoolData> schools)
    {
        Marker? closestMarker = null;
        await foreach (var marker in MarkerService!.GetMarkers(box, schools))
        {
            Console.WriteLine(
                "[FindClosestMarker] Marker Properties: "
                    + JsonSerializer.Serialize(marker.Properties)
            );
            if (marker == null)
                continue;

            closestMarker ??= marker;
            double closestDistance = closestMarker.Properties["TotalDistance"];
            double newDistance = marker.Properties["TotalDistance"];

            if (newDistance >= closestDistance)
                marker.PinColor = PinColor.Red;
            else
            {
                closestMarker.PinColor = PinColor.Red;
                await _mapDisplay!.AddOrUpdateMarker(closestMarker); // Update previous closest marker

                marker.PinColor = PinColor.Green;
                closestMarker = marker;
            }

            _menu!.AddMarker(marker);
            await _mapDisplay!.AddOrUpdateMarker(marker);
        }

        return closestMarker?.Properties["TotalDistance"];
    }

    public void SelectMarker(Marker marker) => _menu!.SelectMarker(marker);
}
