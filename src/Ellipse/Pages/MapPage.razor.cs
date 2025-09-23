using System.Diagnostics.CodeAnalysis;
using Ellipse.Components.MapDisplay;
using Ellipse.Components.MarkerMenu;
using Ellipse.Services;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Pages;

partial class MapPage : ComponentBase
{
    private MarkerMenu _menu;
    private MapDisplay _mapDisplay;

    [Inject]
    private MarkerService MarkerService { get; set; }

    [Inject]
    private NavigationManager NavigationManager { get; set; }

    private string _selectedRouteName = "Average Distance";

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await SortMarkers();
    }

    private async Task SortMarkers()
    {
        Marker? closestMarker = null;
        await foreach (Marker marker in MarkerService.GetMarkers())
        {
            closestMarker ??= marker;
            double markerDistance = marker.Properties["Routes"]["Total Distance"].Distance;
            double closestDistance = closestMarker.Properties["Routes"]["Total Distance"].Distance;
            if (markerDistance > closestDistance)
            {
                closestMarker.PinColor = PinColor.Red;

                marker.PinColor = PinColor.Blue;
                closestMarker = marker;
            }
            else
            {
                marker.PinColor = PinColor.Red;
            }

            _menu.AddMarker(marker);
            await _mapDisplay.UpdateOrAddMarker(marker);
        }
    }

    public void SelectMarker(Marker marker) => _menu?.SelectMarker(marker);
}
