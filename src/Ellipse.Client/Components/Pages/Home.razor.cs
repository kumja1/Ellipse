using Microsoft.AspNetCore.Components;

namespace Ellipse.Client.Components.Pages;

partial class Home : ComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; set; }
}
