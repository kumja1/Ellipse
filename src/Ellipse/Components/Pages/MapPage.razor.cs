using System.Net.Http.Json;
using System.Text.Json;
using Ellipse.Components.Shared.Menu;
using Ellipse.Services;
using Ellipse.Common.Utils;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Utils.Extensions;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;
using ValueTaskSupplement;
using Serilog;

namespace Ellipse.Components.Pages;

partial class MapPage : ComponentBase
{
    private Menu? _menu;
    private OpenStreetMap? _map;

    private List<SchoolData>? _schools;
    private BoundingBox _boundingBox;
    private string _selectedRouteName = "Average";

    private readonly Dictionary<int, Layer> _layers = [];
    private int _currentLayerIndex;
    private Layer _currentLayer => _layers[_currentLayerIndex];

    private Coordinate? _closestPoint;
    private bool _loading;

    // private Guid _lastMarkerId = Guid.Empty;
    private DateTime _lastMarkerClick = DateTime.MinValue;

    private const double STEP_SIZE = 0.11;

    [Inject] public HttpClient Http { get; set; }

    [Inject] public SchoolDivisionService? SchoolDivisionService { get; set; }
    

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
#if DEBUG
        Log.Information("OnMapLoaded: Starting map initialization");
#endif
        Log.Information("Fetching markers...");
        _schools = await SchoolDivisionService!.GetAllSchools();
#if DEBUG
        Log.Information("OnMapLoaded: Retrieved {SchoolCount} schools", _schools?.Count ?? 0);
#endif
        _boundingBox = new BoundingBox(_schools.Select(s => s.LatLng));
      await AddLayer();
#if DEBUG
        Log.Information("OnMapLoaded: Map initialization complete");
#endif
        
    }


    private async Task<MarkerResponse?> GetMarker(double x, double y, List<SchoolData> schools)
    {
        try
        {
#if DEBUG
            Log.Information("GetMarker: Requesting marker for coordinates ({X}, {Y}) with {SchoolCount} schools", x, y,
                schools.Count);
#endif
            HttpResponseMessage? response = await CallbackHelper.RetryIfInvalid(
                r => r is { IsSuccessStatusCode: true },
                async _ =>
                    await Http
                        .PostAsJsonAsync("api/marker", new MarkerRequest(schools, new GeoPoint2d(x, y)))
            );

            if (response == null)
            {
#if DEBUG
                Log.Information("GetMarker: Received null response for ({X}, {Y})", x, y);
#endif
                return null;
            }

            MarkerResponse? markerResponse = await response
                .Content.ReadFromJsonAsync<MarkerResponse>();
#if DEBUG
            Log.Information("GetMarker: Successfully retrieved marker response for ({X}, {Y})", x, y);
#endif

            return markerResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling server for marker at ({x},{y}): {ex.Message}");
#if DEBUG
            Log.Information(ex, "GetMarker: Exception occurred for coordinates ({X}, {Y})", x, y);
#endif
            return null;
        }
    }

    private async Task FindClosestMarkers(double step, BoundingBox box, Layer layer)
    {
#if DEBUG
        Log.Information("FindClosestMarkers: Starting with step={Step}, currentLayerIndex={LayerIndex}", step,
            _currentLayerIndex);
#endif
        Marker? closestMarker = null;
        TimeSpan? closestDuration = null;
        DateTime lastUpdate = DateTime.Now;
        _closestPoint = null;
        _loading = true;

        foreach (GeoPoint2d[] chunk in box.GetPoints(step).Chunk(20))
        {
#if DEBUG
            Log.Information("FindClosestMarkers: Processing chunk of {ChunkSize} points", chunk.Length);
#endif
            MarkerResponse?[] responses = await Task.WhenAll(chunk.Select(point =>
                GetMarker(point.Lon, point.Lat, _schools)
            ));

            for (int i = 0; i < responses.Length; i++)
            {
                MarkerResponse? response = responses[i];
                if (response == null)
                {
                    Log.Warning("MarkerResponse is null");
                    continue;
                }

                Coordinate point = new(chunk[i].Lon, chunk[i].Lat);
#if DEBUG
                Log.Information("FindClosestMarkers: Adding marker {MarkerAddress} at ({Lon}, {Lat})", response.Address,
                    point.Longitude, point.Latitude);
#endif
                Marker marker = new(MarkerType.MarkerPin, point, response.Address)
                {
                    Properties =
                    {
                        ["Name"] = response.Address,
                        ["Routes"] = response.Routes,
                        ["TotalDistance"] = response.TotalDistance,
                        ["LayerIndex"] = _currentLayerIndex,
                    }
                };
                TimeSpan duration = response.Routes["Average"].Duration;

                layer?.ShapesList.Add(marker);
                _closestPoint ??= point;
                closestMarker ??= marker;
                closestDuration ??= duration;

                if (!(duration < closestDuration))
                    continue;
#if DEBUG
                Log.Information(
                    "FindClosestMarkers: New closest marker {MarkerAddress} found - Duration: {Duration}, Previous: {PreviousDuration}",
                    marker.Text, duration, closestDuration);
#endif
                lastUpdate = DateTime.Now;
                closestMarker.PinColor = PinColor.Red;
                marker.PinColor = PinColor.Green;

                await layer.UpdateLayer();

                closestMarker = marker;
                closestDuration = duration;
                _closestPoint = point;
            }

            if (lastUpdate - DateTime.Now >= TimeSpan.FromSeconds(25))
                break;
        }

        if (_closestPoint == null)
        {
#if DEBUG
            Log.Information("FindClosestMarkers: No closest point found");
#endif
            return;
        }

        _loading = false;
#if DEBUG
        Log.Information("FindClosestMarkers: Processing nearby markers (within 30 minutes)");
#endif
        foreach (KeyValuePair<Coordinate, Marker> kvp in _menu!.Markers)
        {
            // Only process markers from the current layer
            if (!kvp.Value.Properties.TryGetValue("LayerIndex", out dynamic? value) || (int)value != _currentLayerIndex)
                continue;

            TimeSpan duration = kvp.Value.Properties["Routes"]["Average"].Duration;
            bool isNear = (duration - closestDuration.Value).TotalMinutes <= 30;
            if (!isNear || kvp.Key == _closestPoint)
                continue;

            Log.Information("Marker {MarkerText} is near the best route.", kvp.Value.Text);
#if DEBUG
            Log.Information("FindClosestMarkers: Changing marker {MarkerId} to blue (near best route)", kvp.Key);
#endif
            kvp.Value.PinColor = PinColor.Blue;
            await _map.UpdateShape(kvp.Value);
        }
#if DEBUG
        Log.Information("FindClosestMarkers: Complete");
#endif
    }

    private async Task AddLayer()
    {
        _currentLayerIndex++;
#if DEBUG
        Log.Information("AddLayer: Incremented layer index to {LayerIndex}", _currentLayerIndex);
#endif
        if (!_layers.TryGetValue(_currentLayerIndex, out Layer? layer))
        {
            _layers.Add(_currentLayerIndex, new Layer());
            // Make STEP_SIZE exponential
            await FindClosestMarkers(STEP_SIZE, _boundingBox,
            _currentLayer
            );
        }
        else
            await _map.AddLayer(layer);
#if DEBUG
        Log.Information("AddLayer: Layer {LayerIndex} added", _currentLayerIndex);
#endif
    }

    private async Task RemoveLayer()
    {
        if (_currentLayerIndex > 0)
        {
#if DEBUG
            Log.Information("RemoveLayer: Removing layer {LayerIndex}", _currentLayerIndex);
#endif
            await _map.RemoveLayer(_currentLayer);
            _currentLayerIndex--;
#if DEBUG
            Log.Information("RemoveLayer: Layer removed, new currentLayerIndex={LayerIndex}", _currentLayerIndex);
        }
#endif
    }

    private async Task OnMarkerClicked(Dictionary<string, dynamic> eventData)
    {
        Coordinate point = JsonSerializer.Deserialize<Coordinate>(eventData["lngLat"]);
#if DEBUG
        Log.Information("OnMarkerClicked: Marker at ({Lon}, {Lat})", point.Longitude,
            point.Latitude);
#endif

        _menu!.SelectMarker(point);
        if (_loading)
            return;

        if ((DateTime.Now - _lastMarkerClick).TotalMilliseconds <= 500 && point.Within(_closestPoint.Value))
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
}