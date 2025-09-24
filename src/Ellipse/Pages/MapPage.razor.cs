using Ellipse.Components.Menu;
using Ellipse.Services;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Pages;

partial class MapPage : ComponentBase
{
    private Menu _menu;

    [Inject]
    private MarkerService? MarkerService { get; set; }

    [Inject]
    private NavigationManager? NavigationManager { get; set; }

    private string _selectedRouteName = "Average Distance";
    private readonly List<Marker> _markers = [];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        Marker? closestMarker = null;
        foreach (var marker in await MarkerService!.GetMarkers())
        {
            if (marker == null)
                continue;

            closestMarker ??= marker;
            double currentDistance = (double)
                closestMarker.Properties["Routes"]["Total Distance"].Distance;
            double newDistance = (double)marker.Properties["Routes"]["Total Distance"].Distance;
            if (newDistance >= currentDistance)
                continue;
                
            closestMarker.PinColor = PinColor.Blue;
            marker.PinColor = PinColor.Green;
            closestMarker = marker;
        }
    }

    public void SelectMarker(Marker marker) => _menu.SelectMarker(marker);
}
