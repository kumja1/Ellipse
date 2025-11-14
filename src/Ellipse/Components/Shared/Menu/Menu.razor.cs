using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using OpenLayers.Blazor;

namespace Ellipse.Components.Shared.Menu;

public partial class Menu : ComponentBase
{
    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public bool IsList { get; set; }

    [Parameter]
    public RenderFragment<(
        Marker Marker,
        Dictionary<string, (double Distance, TimeSpan Duration)>? Routes
    )> SingleItem
    { get; set; }

    [Parameter]
    public RenderFragment<(Marker Marker, Dictionary<string, (double Distance, TimeSpan Duration)> Routes)> ListItem { get; set; }

    [Parameter]
    public string Class { get; set; }

    private string _class =>
        CssBuilder
            .Default("shadow-2xl bg-white rounded-r-lg overflow-auto")
            .AddClass(Class, !string.IsNullOrWhiteSpace(Class))
            .Build();

    private readonly List<Marker> _markers = [];

    private Marker? SelectedMarker;

    private Dictionary<string, (double Distance, TimeSpan Duration)>? SelectedMarkerRoutes;


    public void ToggleMenuOpen() => Open = !Open;

    public void ToggleMenuView() => IsList = !IsList;

    public void AddMarker(Marker marker) => _markers.Add(marker);

    public void SelectMarker(Marker marker)
    {
        SelectedMarker = marker;
        foreach (KeyValuePair<string, dynamic> kvp in marker.Properties)
        {
            Console.WriteLine($"Marker Property {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine($"Marker Has Routes: {marker.Properties.ContainsKey("Routes")}");
        SelectedMarkerRoutes = marker.Properties["Routes"];
        IsList = false;
        StateHasChanged();
    }
}
