using Microsoft.AspNetCore.Components;
using Ellipse.Models;
using Ellipse.Services;

namespace Ellipse.Pages;

partial class Home : ComponentBase
{
    private List<PointInfo> _markers;

    [Inject]
    private MapService MapService { get; set; }


    protected override async Task OnInitializedAsync()
    {
        _markers = await MapService.GetAverageDistances();
        await base.OnInitializedAsync();
    }
}