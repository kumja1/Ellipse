using Ellipse.Server.Services;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Ellipse.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequestTimeout("ResponseTimeout")]
internal class SchoolsController(GeoService geoService) : ControllerBase
{
    // POST api/schools/get-schools
    [HttpPost("get-schools")]
    public async Task<IActionResult> GetSchools(
        [FromForm] int divisionCode,
        [FromForm] bool overrideCache = false
    )
    {
        if (divisionCode <= 0)
            return BadRequest("Error: Missing or invalid required field 'divisionCode'.");

        var result = await WebScraperService.StartNewAsync(divisionCode, overrideCache, geoService);
        return Content(result, "application/json");
    }
}
