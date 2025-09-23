using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using OpenLayers.Blazor;

namespace Ellipse.Components.MarkerMenu;

partial class MarkerMenu : ComponentBase
{
    [Parameter]
    public bool MenuOpen { get; set; }

    [Parameter]
    public bool IsList { get; set; }

    [Parameter]
    public RenderFragment<(
        Marker? Marker,
        Dictionary<string, (double Distance, string Duration)>? Routes
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

    private readonly List<Marker> _markers = [];

    public Marker? SelectedMarker { get; private set; }

    public Dictionary<string, (double Distance, string Duration)>? SelectedMarkerRoutes =>
        SelectedMarker?.Properties["Routes"]
        as Dictionary<string, (double Distance, string Duration)>;

    public void ToggleMenu() => MenuOpen = !MenuOpen;

    public void ToggleMode() => IsList = !IsList;

    public void SelectMarker(Marker marker)
    {
        SelectedMarker = marker;
        IsList = false;
    }

    public void AddMarker(Marker marker)
    {
        _markers.Add(marker);
        StateHasChanged();
    }
}
