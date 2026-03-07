using Ellipse.Common.Models.Directions;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using OpenLayers.Blazor;
using Serilog;

namespace Ellipse.Client.Components.Layout;

public partial class Menu : ComponentBase
{
    private enum SortBy
    {
        None,
        Distance,
        Duration
    }

    [Parameter]
    public RenderFragment<(
        Marker Marker,
        Dictionary<string, SchoolRoute>? Routes
        )> SingleItem { get; set; }

    [Parameter]
    public RenderFragment<(string MarkerName, Coordinate MarkerCoordinate, double MarkerAverageDistance, TimeSpan
        MarkerAverageDuration)> ListItem { get; set; }

    [Parameter] public string Class { get; set; }

    private bool _isList = true;
    private SortBy _sortBy = SortBy.None;

    private List<KeyValuePair<Coordinate, Marker>> _sortedMarkers;

    private List<KeyValuePair<Coordinate, Marker>> SortedMarkers => _sortedMarkers = _sortBy switch
    {
        SortBy.Distance => _markers.OrderBy(kvp => (double)kvp.Value.Properties["Routes"]["Average"].Distance).ToList(),
        SortBy.Duration => _markers.OrderBy(kvp => (TimeSpan)kvp.Value.Properties["Routes"]["Average"].Duration)
            .ToList(),
        _ => _markers.ToList()
    };

    private string _containerClass =>
        CssBuilder
            .Default("fixed left-0 top-0 z-30 h-full flex items-start overflow-hidden")
            .AddClass(Class, !string.IsNullOrWhiteSpace(Class))
            .Build();

    private readonly Dictionary<Coordinate, Marker> _markers = [];
    private Marker? _selectedMarker;
    public bool Open;


    public void ToggleOpen(Coordinate coordinate = default)
    {
        Log.Information("Before Toggle: {Open}", Open);
        Open = !Open;
        Log.Information("After Toggle: {Open}", Open);

        if (!_markers.TryGetValue(coordinate, out Marker? marker))
            return;

        _selectedMarker = marker;
    }

    public void Add(Coordinate coordinate, Marker marker)
    {
        _markers[coordinate] = marker;
        StateHasChanged();
    }

    public bool ContainsKey(Coordinate coordinate) => _markers.ContainsKey(coordinate);
}