// File: Controllers/MarkerController.cs
using Ellipse.Common.Models.Markers;
using Ellipse.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ellipse.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarkerController(MarkerService markerService) : ControllerBase
{
    // POST api/marker
    [HttpPost]
    public async Task<IActionResult> PostMarker([FromBody] MarkerRequest request)
    {
        MarkerResponse? response = await markerService.GetMarker(request);
        if (response == null)
            return NotFound();
        return Ok(response);
    }
}
