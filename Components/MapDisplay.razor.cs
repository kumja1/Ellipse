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
            await foreach (var marker in Markers)
            {
                _map.MarkersList.Add(marker);
            }

            _map.MarkersList.Sort((x, y) => (((double Distance, string Duration))x.Properties["Distances"]["Average Distance"]).Distance.CompareTo((((double Distance, string Duration))y.Properties["Distances"]["Average Distance"]).Distance));
            ((Marker)_map.MarkersList[0]).PinColor = PinColor.Red;
            await InvokeAsync(StateHasChanged);
        }
    }



}
