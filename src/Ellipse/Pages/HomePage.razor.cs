using Ellipse.Components.MarkerMenu;
using Ellipse.Services;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Pages;

partial class HomePage : ComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; set; }
}
