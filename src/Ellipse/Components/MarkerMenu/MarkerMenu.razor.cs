using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MarkerMenu;

partial class MarkerMenu : ComponentBase
{
    [Parameter]
    public bool DrawerOpen { get; set; }

    [Parameter]
    public EventCallback<bool> DrawerOpenChanged { get; set; }

    [Parameter]
    public List<Marker>? Markers { get; set; }

    [Parameter]
    public Action? OnButtonClick { get; set; }

    [Parameter]
    public bool IsListMode { get; set; }

    private string SelectedRouteName = "Average Distance";

    private Marker? CurrentMarker { get; set; }

    private Dictionary<string, (double Distance, string Duration)>? Routes =>
        CurrentMarker?.Properties["Routes"]
        as Dictionary<string, (double Distance, string Duration)>;

    private (double Distance, string Duration) SelectedRouteProps =>
        Routes != null && Routes.TryGetValue(SelectedRouteName, out var value)
            ? value
            : (Distance: 0d, Duration: string.Empty);

    public void CloseDrawer() => DrawerOpen = false;

    public void ToggleList() => IsListMode = !IsListMode;

    public void SelectMarker(Marker marker)
    {
        CurrentMarker = marker;
        IsListMode = false;
    }

    private static string FormatTimeSpan(TimeSpan timeSpan) =>
        $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
}
