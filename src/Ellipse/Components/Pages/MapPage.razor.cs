using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using OpenLayers.Blazor;
using Ellipse.Services;
using Ellipse.Common.Utils;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Components.Layout;
using Microsoft.AspNetCore.Components;
using Serilog;

namespace Ellipse.Components.Pages;

[SuppressMessage("Usage", "BL0005:Component parameter should not be set outside of its component.")]
partial class MapPage : ComponentBase, IDisposable
{
    private Menu _menu;
    private Map _map;

    private SchoolData[]? _schools;
    private string _selectedRouteName = "Average";

    private int _currentLayerIndex;
    private readonly Layer?[] _layers = new Layer[3];

    private Marker? _closestMarker;
    private readonly Coordinate _virginiaMin = new(-83.675395, 36.540738);
    private readonly Coordinate _virginiaMax = new(-75.242266, 39.466012);

    private bool _loading;
    private readonly CancellationTokenSource _cts = new();

    [Inject] public HttpClient HttpClient { get; set; }

    [Inject] public SchoolDivisionService? SchoolDivisionService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Log.Information("OnMapLoaded: Starting map initialization");

        _schools = await SchoolDivisionService!.GetAllSchools();
        Log.Information("OnMapLoaded: Retrieved {SchoolCount} schools", _schools?.Length ?? 0);
        Log.Information("OnMapLoaded: Map initialization complete");
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        if (_schools != null)
        {
            BoundingBox bounds = new(_schools.Select(s => s.LatLng).ToList());
            await GetMarkers(bounds);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task GetMarkers(BoundingBox box)
    {
        try
        {
            if (_cts.IsCancellationRequested)
                return;

            if (_layers[_currentLayerIndex] != null)
            {
                await _map.AddLayer(_layers[_currentLayerIndex]!);
                Log.Information("GetMarkers: Added existing layer {LayerIndex}", _currentLayerIndex);
                return;
            }

            double step = _currentLayerIndex switch
            {
                0 => 0.11, // ~12 km - Division-wide search
                1 => 0.02, // ~2 km - Zone-level search
                2 => 0.005, // ~500 m - District-level search
                3 => 0.001, // ~100 m - Site-level search (STOP HERE)
                _ => 0.001 // Don't go smaller - not useful for buildings
            };

            Log.Information("GetMarkers: Starting with step={Step}, currentLayerIndex={LayerIndex}", step,
                _currentLayerIndex);

            _loading = true;
            StateHasChanged();

            Layer layer = new();
            await _map.AddLayer(layer);

            TimeSpan closestDuration = TimeSpan.Zero;
            DateTime lastUpdate = DateTime.Now;
            foreach (LngLat[] chunk in box
                         .GetPoints(step).Where(point =>
                             point.Lat > _virginiaMin.Latitude && point.Lat < _virginiaMax.Latitude &&
                             point.Lng > _virginiaMin.Longitude && point.Lng < _virginiaMax.Longitude
                         )
                         .Chunk(16))
            {
                Log.Information("GetMarkers: Processing chunk of {ChunkSize} points", chunk.Length);
                if (_cts.IsCancellationRequested || DateTime.Now - lastUpdate >= TimeSpan.FromSeconds(600))
                    break;

                HttpResponseMessage? httpResponse = await Retry.RetryIfResponseFailed(async _ =>
                    await HttpClient
                        .PostAsJsonAsync("api/marker/batch", new BatchMarkerRequest(chunk, _schools!), _cts.Token)
                );

                if (httpResponse == null)
                {
                    Log.Warning("GetMarkers: Failed to retrieve markers");
                    continue;
                }

                List<MarkerResponse?>? responses =
                    await httpResponse.Content.ReadFromJsonAsync<List<MarkerResponse?>>();

                if (responses == null)
                {
                    Log.Warning("GetMarkers: Failed to deserialize marker responses");
                    continue;
                }

                Log.Information("GetMarkers: Received {ResponseCount} responses", responses.Count);
                Marker[] markers = ArrayPool<Marker>.Shared.Rent(responses.Count);
                for (int i = 0; i < responses.Count; i++)
                {
                    Coordinate coord = new(chunk[i].Lng, chunk[i].Lat);
                    MarkerResponse? response = responses[i];

                    if (response == null)
                    {
                        Log.Warning("MarkerResponse for ({Coord}) is null.", coord);
                        continue;
                    }

                    Log.Information("GetMarkers: Adding marker {MarkerAddress} at ({Lng}, {Lat})", response.Address,
                        coord.Longitude, coord.Latitude);

                    Marker marker = new(MarkerType.MarkerPin, coord, response.Address)
                    {
                        Properties =
                        {
                            ["Routes"] = response.Routes.ToFrozenDictionary(),
                            ["TotalDistance"] = response.TotalDistance,
                        }
                    };

                    markers[i] = marker;
                    TimeSpan duration = response.Routes["Average"].Duration;
                    if (duration < closestDuration)
                    {
#if DEBUG
                        Log.Information(
                            "GetMarkers: New closest marker found - Duration: {Duration}, Previous: {PreviousDuration}",
                            duration, closestDuration);
#endif
                        _closestMarker?.PinColor = PinColor.Red;
                        marker.PinColor = PinColor.Green;

                        _closestMarker = marker;
                        closestDuration = duration;
                    }

                    lastUpdate = DateTime.Now;
                }

                layer.ShapesList.AddRange(markers);
                ArrayPool<Marker>.Shared.Return(markers, clearArray: true);
            }

            if (_closestMarker == null)
            {
#if DEBUG
                Log.Information("GetMarkers: No closest point found");
#endif
                return;
            }

            Log.Information("GetMarkers: Processing nearby markers (within 30 minutes)");
            foreach (Marker marker in layer.ShapesList.Cast<Marker>())
            {
                if (_cts.IsCancellationRequested)
                    return;

                TimeSpan duration = marker.Properties["Routes"]["Average"].Duration;
                bool isNear = (duration - closestDuration).TotalMinutes <= 30;
                if (!isNear || marker == _closestMarker)
                    continue;

                Log.Information("Marker {MarkerText} is near the best route.", marker.Text);
                marker.PinColor = PinColor.Blue;
            }

            _layers[_currentLayerIndex] = layer;
            _currentLayerIndex++;
            Log.Information("GetMarkers: Complete");
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task RemoveLayer()
    {
        Log.Information("RemoveLayer: Removing layer {LayerIndex}", _currentLayerIndex);
        Layer? layer = _layers[_currentLayerIndex];
        if (layer == null)
            return;

        await _map.RemoveLayer(layer);
        if (_currentLayerIndex > 0)
        {
            _currentLayerIndex--;
            Log.Information("RemoveLayer: Layer removed, new currentLayerIndex={LayerIndex}", _currentLayerIndex);
        }
    }

    private async Task OnDoubleClicked(Coordinate coordinate)
    {
        Log.Information("OnDoubleClicked: Marker at ({Lng}, {Lat})", coordinate.Longitude,
            coordinate.Latitude);

        if (_loading || _cts.IsCancellationRequested)
            return;

        if (!_menu.ContainsKey(coordinate))
            return;

        double newRadius = _currentLayerIndex switch
        {
            0 => 0, // Full division (handled by school bounds)
            1 => 10000, // 10 km
            2 => 3000, // 3 km
            3 => 1000, // 1 km (final refinement)
            _ => 1000
        };

        BoundingBox box = new(new LngLat(coordinate.Longitude, coordinate.Latitude),
            newRadius
        );

        await GetMarkers(box);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Log.Information("Dispose: Cleaning up MapPage resources");
        _cts.Cancel();
        _cts.Dispose();

        Log.Information("Dispose: MapPage disposal complete");
    }
}