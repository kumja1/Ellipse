using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;
using Ellipse.Services;

namespace Ellipse.Pages;

partial class Home : ComponentBase
{
    private IAsyncEnumerable<Marker> _markers;

    [Inject]
    private SiteFinder SchoolSiteFinder { get; set; }


    [Inject]
    private NavigationManager NavigationManager { get; set; }


    private bool _drawerOpen = false;

    private Marker _selectedMarker { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _markers = SchoolSiteFinder.GetMarkers();
        await base.OnInitializedAsync();
    }

    public void OnMarkerItemClick(Marker marker)
    {
        _selectedMarker = marker;
       if (!_drawerOpen) OpenDrawer();
    }

    public void GoToLandSearch() => NavigationManager.NavigateTo($"https://www.landsearch.com/properties/{_selectedMarker.Properties["Name"]}");


    public void OpenDrawer() => _drawerOpen = true;
}