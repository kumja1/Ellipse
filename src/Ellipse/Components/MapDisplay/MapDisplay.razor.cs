using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public required Action<Marker> OnMarkerClick { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private OpenStreetMap Map { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public async ValueTask AddOrUpdateMarker(Marker marker)
    {
        if (Map.MarkersList.Contains(marker))
            await Map.UpdateShape(marker);
        else
            Map.MarkersList.Add(marker);
    }

    public async ValueTask SelectMarker(Marker marker) =>
        await Map.SetCoordinates(marker, marker.Coordinates);
}
