using Microsoft.AspNetCore.Components;
using Ellipse.Models;
using dymaptic.GeoBlazor.Core.Model;

namespace Ellipse.Pages;

partial class Home : ComponentBase
{
    private List<(string Name, Coordinate Coordinate, double AverageDistance)> _markers;

    [Inject]
    private MapService MapService { get; set; }

    [Inject]
    private AuthenticationManager AuthenticationManager { get; set; }


    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        AuthenticationManager.ApiKey = "AAPTxy8BH1VEsoebNVZXo8HurF2AjPfqFkkLS9gVkfC1A58o2GsRarUScQB7x2syKq4GXJNmE1ruLx5Xr2Xt9vpN3R54ShB0ZpihMkOcmgu5uTiEEqj3KYn4lW5qeNvZhYaKEJv_WW1hDbPuqF1bPV3ZeTR_t_dpmm5AcEgOr0KFmL1DiNC82WdnC8htrAwksy245KdSWs98El4k_HNDif40UJcvZG0lHcGhvbaosuox9os.AT1_KFYwhQ7B";
        AuthenticationManager.AppId = "7NlwCJHyKFYwhQ7B";
        await AuthenticationManager.Initialize();
        _markers = await MapService.GetAverageDistances();
        
        Console.WriteLine("Markers:" + _markers == null);
        
    }


}