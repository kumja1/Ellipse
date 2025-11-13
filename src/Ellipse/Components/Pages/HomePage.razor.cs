using Microsoft.AspNetCore.Components;

namespace Ellipse.Pages;

partial class HomePage : ComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; set; }
}
