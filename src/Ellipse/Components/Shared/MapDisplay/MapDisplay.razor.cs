using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;
using Serilog;

namespace Ellipse.Components.Shared.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public Action<Marker> OnMarkerClick { get; set; }

    public OpenStreetMap Map { get; set; }

    public async ValueTask AddOrUpdateMarker(Marker marker)
    {
        try
        {
            if (!Map.MarkersList.Contains(marker))
            {
                Map.MarkersList.Add(marker);
                return;
            }

            await Map.UpdateShape(marker);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error updating marker");
        }
    }
}
