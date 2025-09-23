using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public required Action<Marker> OnMarkerClick { get; set; }

    private OpenStreetMap _map { get; set; }

    public async ValueTask UpdateOrAddMarker(Marker marker)
    {
        if (!_map.MarkersList.Contains(marker))
        {
            _map.MarkersList.Add(marker);
            return;
        }

        await _map.UpdateShape(marker);
    }

    public async ValueTask SelectMarker(Marker marker) =>
        await _map.SetCoordinates(marker, marker.Coordinates);
}
