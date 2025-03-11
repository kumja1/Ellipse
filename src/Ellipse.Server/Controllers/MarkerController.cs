// File: Controllers/MarkerController.cs
using Microsoft.AspNetCore.Mvc;
using Ellipse.Server.Services;
using Ellipse.Common.Models.Markers;

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
