// File: Controllers/MarkerController.cs
using Ellipse.Common.Models.Markers;
using Ellipse.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Ellipse.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[OutputCache(PolicyName = "PostCachingPolicy")]
public class MarkerController(MarkerService markerService) : ControllerBase
{
    // POST api/marker
    [HttpPost]
    public async Task<IActionResult> PostMarker(
        [FromBody] MarkerRequest request,
        [FromQuery] bool overwriteCache = false
    )
    {
        MarkerResponse? response = await markerService.GetMarker(request, overwriteCache);
        if (response == null)
            return NotFound();
        return Ok(response);
    }
}
