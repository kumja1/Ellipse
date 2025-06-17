using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;
using OpenLayers.Blazor;

namespace Ellipse.Components.MarkerMenu;

partial class MarkerMenu : ComponentBase
{
    [Parameter]
    public bool MenuOpen { get; set; }

    [Parameter]
    public Action? OnButtonClick { get; set; }

    [Parameter]
    public bool IsListMode { get; set; }

    [Parameter]
    public string Class { get; set; }

    private string _class =>
        CssBuilder
            .Default("shadow-2xl bg-white rounded-r-lg overflow-auto")
            .AddClass(Class, !string.IsNullOrWhiteSpace(Class))
            .Build();

    private string _selectedRouteName = "Average Distance";

    private readonly List<Marker> _markers = [];

    private Marker? CurrentMarker;

    private Dictionary<string, (double Distance, string Duration)>? Routes =>
        CurrentMarker?.Properties["Routes"]
        as Dictionary<string, (double Distance, string Duration)>;

    private (double Distance, string Duration) SelectedRouteProps =>
        Routes != null && Routes.TryGetValue(_selectedRouteName, out var value)
            ? value
            : (Distance: 0d, Duration: string.Empty);

    public void ToggleMenu() => MenuOpen = !MenuOpen;

    public void ToggleMode() => IsListMode = !IsListMode;

    public void SelectMarker(Marker marker)
    {
        CurrentMarker = marker;
        IsListMode = false;
    }

    public void AddMarker(Marker marker)
    {
        _markers.Add(marker);
        StateHasChanged();
    }
}
