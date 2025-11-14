using Microsoft.AspNetCore.Components;

namespace Ellipse.Components.Pages;

partial class HomePage : ComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; set; }
}
