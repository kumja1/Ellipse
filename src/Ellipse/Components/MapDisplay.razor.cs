using Ellipse.Extensions;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components;

public partial class MapDisplay
{
    [Parameter] public required IAsyncEnumerable<Marker> Markers { get; set; }

    [Parameter] public required Action<Marker> OnMarkerClick { get; set; }

    private OpenStreetMap _map { get; set; }

    protected async override Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (Markers != null)
        {
            Marker? lastMarker = null;
            await foreach (var marker in Markers)
            {
                var distance = marker.Properties["Distances"]["Average Distance"].Distance;
                if (lastMarker != null && marker.Properties["Distances"]["Average Distance"].Distance < lastMarker.Properties["Distances"]["Average Distance"].Distance)
                {
                    lastMarker.PinColor = PinColor.Blue;
                }
                lastMarker = marker;
                marker.PinColor = PinColor.Red;
                _map.MarkersList.Add(marker);
            }
            await InvokeAsync(StateHasChanged);
        }
    }



}
