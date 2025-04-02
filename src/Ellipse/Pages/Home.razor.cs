using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;
using Ellipse.Services;
using Ellipse.Components;

namespace Ellipse.Pages;

partial class Home : ComponentBase
{
    private IAsyncEnumerable<Marker> _markers;

    private MarkerMenu _menu;

    [Inject]
    private SiteFinderService _schoolSiteFinder { get; set; }

    [Inject]
    private NavigationManager _navigationManager { get; set; }

    private Marker _selectedMarker { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _markers = _schoolSiteFinder.GetMarkers();
        await base.OnInitializedAsync();
    }

    public void OnMarkerItemClick(Marker marker) {
        _selectedMarker = marker;
        _menu.SelectMarker(marker);
    }

    public void GoToLandSearch() => _navigationManager.NavigateTo($"https://www.landsearch.com/properties/{_selectedMarker.Properties["Name"]}");


}