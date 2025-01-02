using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components;

public partial class MapDisplay
{
    [Parameter] public required IEnumerable<Marker> Markers { get; set; }

    [Parameter] public required Action<Marker> OnMarkerClick { get; set; }
    
    private OpenStreetMap _map { get; set; }



    protected async override Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (Markers != null)
        {
            foreach (var marker in Markers)
            {
                _map.MarkersList.Add(marker);
            }
        }
    }



}
