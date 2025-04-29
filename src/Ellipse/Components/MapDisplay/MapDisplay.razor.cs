using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public required Action<Marker> OnMarkerClick { get; set; }

    private OpenStreetMap _map { get; set; }

    public void AddMarker(Marker marker) => _map.MarkersList.Add(marker);

    public async void SelectMarker(Marker marker) =>
        await _map.SetCoordinates(marker, marker.Coordinates);
}
