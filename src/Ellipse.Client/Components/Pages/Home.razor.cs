using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using Ellipse.Client.Components.Layout;
using Ellipse.Client.Services;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Directions;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;
using Serilog;

namespace Ellipse.Client.Components.Pages;

partial class Home : ComponentBase, IDisposable
{
    private Menu _menu;
    private Map _map;

    private SchoolData[]? _schools;
    private string _selectedRouteName = "Average";

    private int _currentLayerIndex;
    private int _run;
    private readonly Layer?[] _layers = new Layer[3];

    private readonly Coordinate _virginiaMin = new(-83.675395, 36.540738);
    private readonly Coordinate _virginiaMax = new(-75.242266, 39.466012);

    private bool _loading;
    private readonly CancellationTokenSource _cts = new();

    [Inject] public HttpClient HttpClient { get; set; }

    [Inject] public SchoolDivisionService? SchoolDivisionService { get; set; }


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await InvokeAsync(async () => _schools = await SchoolDivisionService.GetAllSchools());

        if (_schools != null)
        {
            await GetMarkers([.. _schools.Select(s => s.LngLat)]);
        }
    }

    private async Task GetMarkers(LngLat[] points)
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
                0 => 0.1,
                1 => 0.11,
                2 => 0.02,
                3 => 0.005,
                _ => 0.001,
            };

            Log.Debug("GetMarkers: Starting with step={Step}, currentLayerIndex={LayerIndex}", step,
                _currentLayerIndex);

            _loading = true;
            StateHasChanged();

            Layer layer = new()
            {
                LayerType = LayerType.Vector
            };
            await _map.AddLayer(layer);
            StateHasChanged();

            TimeSpan closestDuration = TimeSpan.MaxValue;
            Marker? closestMarker = null;

            LngLat min = points.Min();
            LngLat max = points.Max();

            IEnumerable<LngLat[]> chunks = GetPointsWithin(min, max, step, _ => true)
            .Chunk(8);

            foreach (LngLat[] chunk in chunks)
            {
                Log.Debug("GetMarkers: Processing chunk of {ChunkSize} points", chunk.Length);
                if (_cts.IsCancellationRequested)
                    break;

                HttpResponseMessage? httpResponse = await Retry.RetryIfResponseFailed(async _ =>
                    await HttpClient
                        .PostAsJsonAsync("http://localhost:5291/api/marker/batch", new BatchMarkerRequest(chunk, _schools!), _cts.Token),
                        maxRetries: 20
                );

                if (httpResponse == null)
                {
                    Log.Warning("GetMarkers: Failed to retrieve markers");
                    continue;
                }

                Log.Information("GetMarker: HttpResponse Content {ResponseContent}", await httpResponse.Content.ReadAsStringAsync());
                MarkerResponse?[]? responses =
                    await httpResponse.Content.ReadFromJsonAsync<MarkerResponse?[]>(cancellationToken: _cts.Token);

                if (responses == null)
                {
                    Log.Warning("GetMarkers: Failed to deserialize marker responses");
                    continue;
                }

                Log.Debug("GetMarkers: Received {ResponseCount} responses", responses.Length);
                List<Marker> markers =  new(responses.Length);
                for (int i = 0; i < responses.Length; i++)
                {
                    MarkerResponse? response = responses[i];
                    if (response == null)
                    {
                        Log.Warning("MarkerResponse at index {Index}.", i);
                        continue;
                    }

                    Coordinate coord = new(response.LngLat.Lng, response.LngLat.Lat);
                    Log.Information("GetMarkers: Adding marker {MarkerAddress} at ({Lng}, {Lat})", response.Address,
                        coord.Longitude, coord.Latitude);

                    Marker marker = new(MarkerType.MarkerPin, coord, response.Address)
                    {
                        Properties =
                        {
                            ["Routes"] = response.Routes.ToFrozenDictionary(),
                        }
                    };

                    markers.Add(marker);
                    TimeSpan duration = response.Routes["Average"].Duration;
                    if (duration < closestDuration)
                    {
                        Log.Debug(
                            "GetMarkers: New closest marker found - Duration: {Duration}, Previous: {PreviousDuration}",
                            duration, closestDuration);

                        closestMarker?.PinColor = PinColor.Red;
                        closestMarker?.UpdateShape();
                        marker.PinColor = PinColor.Green;

                        closestMarker = marker;
                        closestDuration = duration;
                    }
                }

                foreach (Marker? marker1 in markers)
                {
                    if (marker1 == null)
                    {
                        Log.Information("Marker is null");
                        continue;
                    }
                    Log.Information("GetMarker: Marker at {MarkerCoord} is {MarkerColor}", marker1.Coordinate, marker1.PinColor);
                }

                layer.ShapesList.AddRange(markers.Take(responses.Length).Where(m => m != null));
                Log.Information("GetMarkers: Layer {LayerId} contains {Count1}/{Count2}", _currentLayerIndex, layer.ShapesList.Count, markers.Count);

            }

            if (closestMarker == null)
            {
                Log.Information("GetMarkers: Closest marker could not be found");
                return;
            }

            Log.Information("GetMarkers: Processing nearby markers (within 30 minutes)");
            foreach (Marker marker in layer.ShapesList.Cast<Marker>())
            {
                if (_cts.IsCancellationRequested)
                    return;

                FrozenDictionary<string, SchoolRoute>? routes = marker.Properties.GetValueOrDefault("Routes");
                if (routes == null)
                {
                    Log.Warning("'Routes' for {MarkerCoord} could not be found", marker.Coordinate);
                    continue;
                }

                TimeSpan duration = routes["Average"].Duration;
                bool isNear = (duration - closestDuration).TotalMinutes <= 30;
                if (!isNear || marker == closestMarker)
                    continue;

                Log.Information("Marker {MarkerCoord} is near the best route.", marker.Coordinate);
                marker.PinColor = PinColor.Blue;
                marker.UpdateShape();
            }

            _layers[_currentLayerIndex] = layer;
        }
        catch (Exception e)
        {
            Log.Error("An error occured: {Exception}", e);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private IEnumerable<LngLat> GetPointsWithin(LngLat min, LngLat max, double step, Func<LngLat, bool> include)
    {
        for (double y = min.Lat; y <= max.Lat; y += step)
            for (double x = min.Lng; x < max.Lng; x += step)
            {
                LngLat lngLat = new(x, y);
                if (include(lngLat))
                    yield return lngLat;
            }
    }


    private async Task RemoveLayer()
    {
        Log.Information("RemoveLayer: Removing layer {LayerIndex}", _currentLayerIndex);
        Layer? layer = _layers[_currentLayerIndex];
        if (layer == null)
        {
            Log.Information("Layer {LayerIndex} is null", _currentLayerIndex);
            return;
        }

        await _map.RemoveLayer(layer);
        if (_currentLayerIndex > 0)
        {
            _layers[_currentLayerIndex--] = null;
            Log.Debug("RemoveLayer: Layer removed, new currentLayerIndex={LayerIndex}", _currentLayerIndex);
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

        _currentLayerIndex++;

        // BoundingBox box = new(new LngLat(coordinate.Longitude, coordinate.Latitude),
        //     newRadius
        // );

        // await GetMarkers();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Log.Information("Dispose: Cleaning up Map resources");
        _cts.Cancel();
        _cts.Dispose();

        Log.Information("Dispose: Map disposal complete");
    }
}

