using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public required Action<Marker> OnMarkerClick { get; set; }

    [Parameter]
    public required List<Marker> Markers { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private OpenStreetMap Map { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public async ValueTask SelectMarker(Marker marker) =>
        await Map.SetCoordinates(marker, marker.Coordinates);
}
