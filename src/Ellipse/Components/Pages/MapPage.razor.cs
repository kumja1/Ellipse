using System.Net.Http.Json;
using System.Text.Json;
using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Marker;
using Ellipse.Components.Shared.Menu;
using Ellipse.Services;
using Ellipse.Common.Utils;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Utils.Extensions;
using Microsoft.AspNetCore.Components;
using Serilog;

namespace Ellipse.Components.Pages;

partial class MapPage : ComponentBase, IDisposable
{
    private Menu? _menu;
    private MapLibre? _map;

    private SchoolData[]? _schools;
    private string _selectedRouteName = "Average";

    private Listener _clickListener;
    private readonly List<int> _layers = [];

    private int _currentLayerIndex;

    private LngLat? _closestPoint;
    private bool _loading;

    // private Guid _lastMarkerId = Guid.Empty;
    private DateTime _lastMarkerClick = DateTime.MinValue;

    private readonly MapOptions _mapOptions = new()
    {
        MaplibreLogo = false,
        Style = "https://api.maptiler.com/maps/openstreetmap/style.json?key=oNR4taxJ0rX3LX6CqnJd",
        Center = new LngLat(-78, 37),
        MinZoom = 5,
        // DoubleClickZoom = false,
    };

    [Inject] public HttpClient HttpClient { get; set; }

    [Inject] public SchoolDivisionService? SchoolDivisionService { get; set; }

    private async Task OnMapLoaded()
    {
#if DEBUG
        Log.Information("OnMapLoaded: Starting map initialization");
#endif
        Log.Information("Fetching markers...");
        _clickListener = await _map!.AddAsyncListener<Dictionary<string, dynamic>>("click", OnMarkerClicked);
#if DEBUG
        Log.Information("OnMapLoaded: Event listeners registered");
#endif

        _schools = await SchoolDivisionService!.GetAllSchools();
#if DEBUG
        Log.Information("OnMapLoaded: Retrieved {SchoolCount} schools", _schools?.Length ?? 0);
#endif
        BoundingBox bbox = new(_schools.Select(s => s.LatLng));
        Log.Information("Initializing map with bounding box: {BoundingBox}", bbox);
        await GetMarkers(0.11, bbox
        );
#if DEBUG
        Log.Information("OnMapLoaded: Map initialization complete");
#endif
    }

    private async Task GetMarkers(double step, BoundingBox box)
    {
        try
        {
#if DEBUG
            Log.Information("GetMarkers: Starting with step={Step}, currentLayerIndex={LayerIndex}", step,
                _currentLayerIndex);
#endif
            _loading = true;
            StateHasChanged();

            Guid? closestMarkerId = null;
            TimeSpan? closestDuration = null;
            DateTime lastUpdate = DateTime.Now;
            foreach (GeoPoint2d[] row in box.GetPoints(step).Chunk(16))
            {
#if DEBUG
                Log.Information("GetMarkers: Processing chunk of {ChunkSize} points", row.Length);
#endif
                if (DateTime.Now - lastUpdate >= TimeSpan.FromSeconds(10))
                    break;

                HttpResponseMessage? httpResponse = await Retry.RetryIfResponseFailed(async _ =>
                    await HttpClient
                        .PostAsJsonAsync("api/marker/batch", new BatchMarkerRequest(row, _schools))
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

                for (int i = 0; i < responses.Count; i++)
                {
                    GeoPoint2d p = row[i];
                    MarkerResponse? response = responses[i];
                    if (response == null)
                    {
                        Log.Warning("MarkerResponse for ({Point}) is null.", p);
                        continue;
                    }

                    LngLat point = new(row[i].Lon, row[i].Lat);
                    Log.Debug("GetMarkers: Adding marker {MarkerAddress} at ({Lon}, {Lat})", response.Address,
                        point.Longitude, point.Latitude);

                    Guid markerId = await _map!.AddMarker(new MarkerOptions
                    {
                        Color = "red"
                    }, point);

                    _menu.AddMarker(point, new Dictionary<string, dynamic>
                    {
                        ["Name"] = response.Address,
                        // ["Image256Url"] = response.Image256Url,
                        // ["Image1024Url"] = response.Image1024Url,
                        ["Id"] = markerId,
                        ["Routes"] = response.Routes,
                        ["TotalDistance"] = response.TotalDistance,
                        ["LayerIndex"] = _currentLayerIndex,
                        ["Color"] = "red"
                    });

                    TimeSpan duration = response.Routes["Average"].Duration;
                    _closestPoint ??= point;
                    closestMarkerId ??= markerId;
                    closestDuration ??= duration;

                    if (!(duration < closestDuration))
                        continue;

#if DEBUG
                    Log.Information(
                        "GetMarkers: New closest marker found - Duration: {Duration}, Previous: {PreviousDuration}",
                        duration, closestDuration);
#endif
                    await Task.WhenAll(
                        _map.UpdateMarker(new MarkerOptions { Color = "red" }, _closestPoint, closestMarkerId.Value),
                        _map.AddMarker(new MarkerOptions { Color = "green" }, point, markerId));

                    _menu!.UpdateMarker(_closestPoint, "Color", "red");
                    _menu!.UpdateMarker(_closestPoint, "Color", "green");
                    _closestPoint = point;
                    lastUpdate = DateTime.Now;
                    closestMarkerId = markerId;
                    closestDuration = duration;
                }
            }

            if (_closestPoint == null)
            {
#if DEBUG
                Log.Information("GetMarkers: No closest point found");
#endif
                return;
            }

#if DEBUG
            Log.Information("GetMarkers: Processing nearby markers (within 30 minutes)");
#endif
            foreach (KeyValuePair<LngLat, Dictionary<string, dynamic>> kvp in _menu!.GetMarkers())
            {
                // Only process markers from the current layer
                if (!kvp.Value.TryGetValue("LayerIndex", out dynamic? value) || (int)value != _currentLayerIndex)
                    continue;

                TimeSpan duration = kvp.Value["Routes"]["Average"].Duration;
                bool isNear = (duration - closestDuration.Value).TotalMinutes <= 30;
                if (!isNear || kvp.Key == _closestPoint)
                    continue;

                Log.Information("Marker {MarkerText} is near the best route.", kvp.Value["Name"]);
#if DEBUG
                Log.Information("GetMarkers: Changing marker {MarkerId} to blue (near best route)", kvp.Key);
#endif
                await _map.UpdateMarker(new MarkerOptions { Color = "blue" }, kvp.Key, (Guid)kvp.Value["Id"]);
            }
#if DEBUG
            Log.Information("GetMarkers: Complete");
#endif
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task AddLayer()
    {
        _currentLayerIndex++;
#if DEBUG
        Log.Information("AddLayer: Incremented layer index to {LayerIndex}", _currentLayerIndex);
#endif
        if (!_layers.Contains(_currentLayerIndex))
        {
            _layers.Add(_currentLayerIndex);
            double newStep = _currentLayerIndex switch
            {
                0 => 0.11, // ~12 km - Division-wide search (your initial)
                1 => 0.02, // ~2 km - Zone-level search
                2 => 0.005, // ~500 m - District-level search
                3 => 0.001, // ~100 m - Site-level search (STOP HERE)
                _ => 0.001 // Don't go smaller - not useful for buildings
            };

            double newRadius = _currentLayerIndex switch
            {
                0 => 0, // Full division (handled by school bounds)
                1 => 10000, // 10 km
                2 => 3000, // 3 km
                3 => 1000, // 1 km (final refinement)
                _ => 1000
            };
            await _map!.FitBounds(LngLatBounds.FromLngLat(_closestPoint!, newRadius));
#if DEBUG
            Log.Information("AddLayer: Layer {LayerIndex} added with step={Step}, radius={Radius}",
                _currentLayerIndex, newStep, newRadius);
#endif

            BoundingBox box = new(
                new GeoPoint2d(_closestPoint.Longitude, _closestPoint.Latitude),
                newRadius
            );

            await GetMarkers(newStep, box);
        }
        else
        {
            await Task.WhenAll(_menu!.GetMarkers()
                .Where(kvp => kvp.Value["LayerIndex"] == _currentLayerIndex)
                .Select(kvp => _map!.AddMarker(
                    new MarkerOptions { Color = kvp.Value["Color"] },
                    kvp.Key,
                    (Guid)kvp.Value["Id"]
                )));
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task RemoveLayer()
    {
#if DEBUG
        Log.Information("RemoveLayer: Removing layer {LayerIndex}", _currentLayerIndex);
#endif
        await Task.WhenAll(_menu!.GetMarkers().Where(m => m.Value["LayerIndex"] == _currentLayerIndex)
            .Select(m => _map!.RemoveMarker((Guid)m.Value["Id"])));

        if (_currentLayerIndex > 0)
        {
            _currentLayerIndex--;
#if DEBUG
            Log.Information("RemoveLayer: Layer removed, new currentLayerIndex={LayerIndex}", _currentLayerIndex);
        }
#endif
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnMarkerClicked(Dictionary<string, dynamic> eventData)
    {
        LngLat point = JsonSerializer.Deserialize<LngLat>(eventData["lngLat"]);
#if DEBUG
        Log.Information("OnMarkerClicked: Marker at ({Lon}, {Lat})", point.Longitude,
            point.Latitude);
#endif

        _menu!.ToggleView(point);
        if (_loading)
            return;

        if ((DateTime.Now - _lastMarkerClick).TotalMilliseconds <= 500 && point.Within(_closestPoint!))
        {
            Log.Information("Double-clicked on closest marker - zooming to it.");
#if DEBUG
            Log.Information("OnMarkerClicked: Double-click on closest point, fitting bounds and adding layer");
#endif
            await RemoveLayer();
            await AddLayer();
        }

        _lastMarkerClick = DateTime.Now;
    }

    public void Dispose()
    {
#if DEBUG
        Log.Information("Dispose: Cleaning up MapPage resources");
#endif
        if (_map != null)
            _ = _map.DisposeAsync().AsTask();

        _clickListener.Dispose();
#if DEBUG
        Log.Information("Dispose: MapPage disposal complete");
#endif
    }
}