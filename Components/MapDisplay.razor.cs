using dotnet80_example.Models;
using dymaptic.GeoBlazor.Core.Events;
using Microsoft.AspNetCore.Components;
using dymaptic.GeoBlazor.Core.Components.Layers;
using dymaptic.GeoBlazor.Core.Components.Symbols;
using dymaptic.GeoBlazor.Core.Objects;
using dymaptic.GeoBlazor.Core.Components.Popups;
using dymaptic.GeoBlazor.Core.Components.Geometries;

namespace Ellipse.Components;

public partial class MapDisplay
{
    [Parameter] public List<(string Name, Coordinate Coordinate, double AverageDistance)> Markers { get; set; }

    [Parameter] public MapColor MarkerColor { get; set; } = new MapColor(255, 0, 0);

    [Parameter] public SimpleMarkerStyle MarkerStyle { get; set; } = SimpleMarkerStyle.Circle;

    private double _longitude = -98.5795;

    private double _latitude = 39.8283;

    private double _zoom = 4;

    private GraphicsLayer _graphics;

    private const double MinZoom = 3;
    private const double MaxZoom = 10;

    public void OnZoom(MouseWheelEvent args)
    {
        _zoom = args.DeltaY > 0 ? _zoom + 1 : _zoom - 1;
        StateHasChanged();
    }

    public void OnDrag(DragEvent args)
    {
        _longitude = args.Y;
        _latitude = args.X;
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_graphics != null && _zoom >= MinZoom && _zoom <= MaxZoom)
        {
            await _graphics.Add(Markers.Select(marker =>
                new Graphic(
                new Point(marker.Coordinate.Lng, marker.Coordinate.Lat),
                new SimpleMarkerSymbol(color: MarkerColor, size: 10, style: MarkerStyle),
                new PopupTemplate(marker.Name, $"Latitude: {marker.Coordinate.Lat}\nLongitude: {marker.Coordinate.Lng}\nAverage: {marker.AverageDistance}"))
            ));

            await InvokeAsync(StateHasChanged);
        }
    }
}
