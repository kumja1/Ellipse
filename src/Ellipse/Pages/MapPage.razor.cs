using Ellipse.Common.Models;
using Ellipse.Components.MapDisplay;
using Ellipse.Components.Menu;
using Ellipse.Services;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Pages;

partial class MapPage : ComponentBase
{
    private Menu _menu;

    private MapDisplay _mapDisplay;

    [Inject]
    private MarkerService? MarkerService { get; set; }

    [Inject]
    private SchoolDivisionService? SchoolDivisionService { get; set; }

    [Inject]
    private NavigationManager? NavigationManager { get; set; }

    private string _selectedRouteName = "Average Distance";

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
        Marker? closestMarker = null;
        await foreach (var marker in MarkerService!.GetMarkers(box, schools))
        {
            if (marker == null)
                continue;

            closestMarker ??= marker;
            double currentDistance = (double)
                closestMarker.Properties["Routes"]["Total Distance"].Distance;
            double newDistance = (double)marker.Properties["Routes"]["Total Distance"].Distance;

            bool similar = Math.Abs(newDistance - currentDistance) <= 1;
            if (newDistance < currentDistance)
            {
                marker.PinColor = PinColor.Green;
                closestMarker = marker;
            }
            else
                marker.PinColor = similar ? PinColor.Blue : PinColor.Red;

            await _mapDisplay.AddOrUpdateMarker(marker);
            _menu.AddMarker(marker);
        }
    }

    public void SelectMarker(Marker marker) => _menu.SelectMarker(marker);
}
