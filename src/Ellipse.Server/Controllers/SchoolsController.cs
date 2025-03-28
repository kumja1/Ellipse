using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Ellipse.Server.Functions;
using Ellipse.Server.Services;

namespace Ellipse.Server.Controllers;


[ApiController]
[Route("api/[controller]")]
[RequestTimeout("ResponseTimeout")]
public class SchoolsController(GeoService geoService) : ControllerBase
{
    // POST api/schools/get-schools
    [HttpPost("get-schools")]
    public async Task<IActionResult> GetSchools([FromForm] int divisionCode, [FromForm] bool overrideCache = false)
    {
        if (divisionCode <= 0)
            return BadRequest("Error: Missing or invalid required field 'divisionCode'.");
        
        var result = await WebScraper.StartNewAsync(divisionCode, overrideCache, geoService);
        return Content(result, "application/json");
    }
}

