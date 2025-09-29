using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using OpenLayers.Blazor;

namespace Ellipse.Components.Menu;

public partial class Menu : ComponentBase
{
    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public bool IsList { get; set; }

    [Parameter]
    public RenderFragment<(
        Marker Marker,
        Dictionary<string, (double Distance, TimeSpan Duration)> Routes
    )> ItemView { get; set; }

    [Parameter]
    public RenderFragment<List<Marker>> ListView { get; set; }

    [Parameter]
    public string Class { get; set; }

    private string _class =>
        CssBuilder
            .Default("shadow-2xl bg-white rounded-r-lg overflow-auto")
            .AddClass(Class, !string.IsNullOrWhiteSpace(Class))
            .Build();

    public Marker? SelectedMarker { get; private set; }

    public Dictionary<string, (double Distance, TimeSpan Duration)> SelectedMarkerRoutes =>
        SelectedMarker != null && SelectedMarker.Properties.TryGetValue("Routes", out var routes)
            ? routes as Dictionary<string, (double Distance, TimeSpan Duration)>
            : [];

    private readonly List<Marker> _markers = [];

    public void ToggleMenuOpen() => Open = !Open;

    public void ToggleMenuView() => IsList = !IsList;

    public void AddMarker(Marker marker) => _markers.Add(marker);

    public void SelectMarker(Marker marker)
    {
        SelectedMarker = marker;
        IsList = false;
        StateHasChanged();
    }
}
