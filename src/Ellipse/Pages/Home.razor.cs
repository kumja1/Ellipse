using Ellipse.Components.MarkerMenu;
using Ellipse.Services;
using Microsoft.AspNetCore.Components;
using OpenLayers.Blazor;

namespace Ellipse.Pages;

partial class Home : ComponentBase
{
    [Inject] private NavigationManager _navigationManager { get; set; }
    
    private void NavToMap() => _navigationManager.NavigateTo("map");
}
