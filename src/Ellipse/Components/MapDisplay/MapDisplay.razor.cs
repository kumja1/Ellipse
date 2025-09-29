using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public required Action<Marker> OnMarkerClick { get; set; }

    public required OpenStreetMap Map { get; set; }

    public async ValueTask AddOrUpdateMarker(Marker marker)
    {
        try
        {
            if (Map.MarkersList.Contains(marker))
                await Map.UpdateShape(marker);
            else if (marker != null)
                Map.MarkersList.Add(marker);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
