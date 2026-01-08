using Community.Blazor.MapLibre.Models;
using Ellipse.Common.Models.Directions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using MudBlazor.Utilities;
using Serilog;

namespace Ellipse.Components.Shared.Menu;

public partial class Menu : ComponentBase
{
    [Parameter] public bool IsList { get; set; }

    [Parameter]
    public RenderFragment<(
        Dictionary<string, dynamic> Data,
        Dictionary<string, Route>? Routes
        )> SingleItem { get; set; }

    [Parameter]
    public RenderFragment<(string MarkerName, LngLat MarkerLngLat, double MarkerAverageDistance, TimeSpan
        MarkerAverageDuration)> ListItem { get; set; }

    [Parameter] public string Class { get; set; }
    private string _class =>
        CssBuilder
            .Default("flex shadow-2xl bg-white rounded-r-lg overflow-auto")
            .AddClass(Class, !string.IsNullOrWhiteSpace(Class))
            .Build();
    private readonly Dictionary<LngLat, Dictionary<string, dynamic>> _markers = [];
    private Dictionary<string, dynamic>? _selectedMarkerData;
    private Virtualize<KeyValuePair<LngLat, Dictionary<string, dynamic>>> _virtualize;

    private bool _open;


    public void ToggleOpen()
    {
        Log.Information("Before Toggle: {Open}", _open);
        _open = !_open;
        Log.Information("After Toggle: {Open}", _open);
    }

    public void ToggleView(LngLat? point = null)
    {
        IsList = point == null; // List view if null, single view if not
        if (point == null || !_markers.TryGetValue(point, out Dictionary<string, dynamic>? markerData))
        {
            Log.Warning("SelectMarker: Marker not found at point: {Point}", point);
            return;
        }
        _selectedMarkerData = markerData;
        StateHasChanged();
    }
    
    public void AddMarker(LngLat point, Dictionary<string, dynamic> data)
    {
        _markers[point] = data;
        StateHasChanged();
    }

    public void UpdateMarker(LngLat point, string key, dynamic value)
    {
        _markers[point][key] = value;
        StateHasChanged();
    }

    public  Dictionary<LngLat, Dictionary<string,dynamic>> GetMarkers() => _markers;
}