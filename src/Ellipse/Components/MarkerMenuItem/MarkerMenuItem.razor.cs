using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Components.MarkerMenuItem;

partial class MarkerMenuItem
{
    [Parameter] public Marker Marker { get; set; }
    [Parameter] public Action<Marker> OnSelect { get; set; }

}