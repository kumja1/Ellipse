using Ellipse.Common.Models;
using Ellipse.Common.Models.Directions;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using OpenLayers.Blazor;
using Serilog;

namespace Ellipse.Components.Shared.Menu;

public partial class Menu : ComponentBase
{
    [Parameter] public bool Open { get; set; }

    [Parameter] public bool IsList { get; set; }

    [Parameter]
    public RenderFragment<(
        Marker Marker,
        Dictionary<string, Route>? Routes
        )> SingleItem { get; set; }

    [Parameter]
    public RenderFragment<(Coordinate MarkerLngLat, double MarkerAverageDistance, TimeSpan
        MarkerAverageDuration)> ListItem { get; set; }

    [Parameter] public string Class { get; set; }

    private string _class =>
        CssBuilder
            .Default("flex shadow-2xl bg-white rounded-r-lg overflow-auto")
            .AddClass(Class, !string.IsNullOrWhiteSpace(Class))
            .Build();

    public readonly Dictionary<Coordinate, Marker> Markers = [];

    private Marker? _selectedMarker;

    public void ToggleOpen()
    {
        Open = !Open;
        Log.Information("Menu Open: {Open}", Open);
    }

    public void ToggleView() => IsList = !IsList;

    public void SelectMarker(Coordinate point)
    {
        if (!Markers.TryGetValue(point, out Marker? marker))
        {
            Log.Warning("SelectMarker: Marker not found at point: {Point}", point);
            return;
        }

        _selectedMarker = marker;
        IsList = false;
        StateHasChanged();
    }
}