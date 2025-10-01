using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public required Action<Marker> OnMarkerClick { get; set; }

    [Inject]
    private ILogger<MapDisplay> Logger { get; set; }

    public required OpenStreetMap Map { get; set; }

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
            Logger.LogError(e, "Error updating marker");
        }
    }
}
