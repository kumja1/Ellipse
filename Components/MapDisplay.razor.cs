using Ellipse.Models;
using Microsoft.AspNetCore.Components;

namespace Ellipse.Components;

public partial class MapDisplay
{
    [Parameter] public required List<PointInfo> Markers { get; set; } 
}
