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
    private SiteFinderService SchoolSiteFinder { get; set; }

    [Inject]
    private NavigationManager NavigationManager { get; set; }

    private Marker? SelectedMarker { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await SortMarkers();
    }

    private async Task SortMarkers()
    {
        Marker? closestMarker = null;
        await foreach (var marker in SchoolSiteFinder.GetMarkers())
        {
            double markerDistance = marker.Properties["Distances"]["Average Distance"].Distance;

            if (closestMarker == null)
            {
                closestMarker = marker;
                marker.PinColor = PinColor.Blue;
            }
            else
            {
                double closestDistance = closestMarker
                    .Properties["Distances"]["Average Distance"]
                    .Distance;
                if (markerDistance < closestDistance)
                {
                    closestMarker.PinColor = PinColor.Red;
                    marker.PinColor = PinColor.Blue;
                    closestMarker = marker;
                    continue;
                }
                marker.PinColor = PinColor.Red;
            }

            _menu.AddMarker(marker);
            _mapDisplay.AddMarker(marker);
        }
    }

    public void OnMarkerItemClick(Marker marker)
    {
        SelectedMarker = marker;
        _menu?.SelectMarker(marker);
    }
}
