using Ellipse.Common.Models.Directions;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using OpenLayers.Blazor;
using Serilog;

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
        Dictionary<string, Route>? Routes
    )> SingleItem
    { get; set; }

    [Parameter]
    public RenderFragment<(Marker Marker, Dictionary<string, Route> Routes)> ListItem { get; set; }

    [Parameter]
    public string Class { get; set; }

    private string _class =>
        CssBuilder
            .Default("shadow-2xl bg-white rounded-r-lg overflow-auto")
            .AddClass(Class, !string.IsNullOrWhiteSpace(Class))
            .Build();

    private readonly List<Marker> _markers = [];

    private Marker? SelectedMarker;

    private Dictionary<string, Route>? SelectedMarkerRoutes;


    public void ToggleMenuOpen()
    {
        Open = !Open;
        Log.Information("Menu Open: {Open}", Open);
    }

    public void ToggleMenuView() => IsList = !IsList;

    public void AddMarker(Marker marker)
    {
        _markers.Add(marker);
                Log.Information("New marker added to menu: {@Marker}", marker);
    }

    public void SelectMarker(Marker marker)
    {
        SelectedMarker = marker;
        foreach (KeyValuePair<string, dynamic> kvp in marker.Properties)
            Log.Information("Marker Property {@Property}", kvp);

        Log.Information("Selected Marker: {@Marker}", marker);
        if (marker.Properties.TryGetValue("Routes", out var routes))
            SelectedMarkerRoutes = routes;

        IsList = false;
        StateHasChanged();
    }
}
