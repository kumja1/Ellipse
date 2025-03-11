using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using OpenLayers.Blazor;

namespace Ellipse.Components;

partial class Menu : ComponentBase
{

    [Parameter] public bool DrawerOpen { get; set; }
    [Parameter] public EventCallback<bool> DrawerOpenChanged { get; set; }


    [Parameter] public Marker CurrentMarker { get; set; }


    [Parameter] public Action OnButtonClick { get; set; }


    private string SelectedDistanceMode { get; set; } = "Average Distance";

    private Dictionary<string, (double Distance, string Duration)> SelectedMarkerDistances => CurrentMarker.Properties["Distances"];


    public void CloseDrawer() => DrawerOpen = false;

    private static string CleanDuration(string duration) => duration switch
    {
        _ when duration.Contains('|') => duration.Split('|')[1],
        _ => duration
    };

    private static string FormatTimeSpan(TimeSpan timeSpan) => $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
}