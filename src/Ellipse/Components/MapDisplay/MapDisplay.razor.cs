using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MapDisplay;

public partial class MapDisplay
{
    [Parameter]
    public required IAsyncEnumerable<Marker> Markers { get; set; }

    [Parameter]
    public required Action<Marker> OnMarkerClick { get; set; }

    private OpenStreetMap _map { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (Markers != null)
        {
            Marker? closestMarker = null;
            await foreach (var marker in Markers)
            {
                double markerDistance = marker.Properties["Distances"]["Average Distance"].Distance;

                if (closestMarker == null)
                {
                    closestMarker = marker;
                    marker.PinColor = PinColor.Blue;
                }
                else
                {
                    double closestDistance = closestMarker
                        .Properties["Distances"]["Average Distance"]
                        .Distance;
                    if (markerDistance < closestDistance)
                    {
                        closestMarker.PinColor = PinColor.Red;
                        marker.PinColor = PinColor.Blue;
                        closestMarker = marker;
                        continue;
                    }
                    marker.PinColor = PinColor.Red;
                }
                _map.MarkersList.Add(marker);
            }
        }
    }
}
