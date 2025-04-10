// File: Controllers/MarkerController.cs
using Ellipse.Common.Models.Markers;
using Ellipse.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ellipse.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarkerController(MarkerService markerService) : ControllerBase
    {
        // POST api/marker?x=...&y=...
        [HttpPost("get-markers")]
        public async Task<IActionResult> PostMarker([FromBody] MarkerRequest request)
        {
            var response = await markerService.GetMarkerByLocation(request);
            if (response == null)
                return NotFound();
            return Ok(response);
        }
    }
}
