using Ellipse.Server.Services;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Ellipse.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[OutputCache(Duration = 1200)]
public sealed class SchoolsController(SchoolsScraperService scraperService) : ControllerBase
{
    // GET api/schools
    [HttpGet]
    public async Task<IActionResult> GetSchools(
        [FromQuery] int divisionCode,
        [FromQuery] bool overrideCache = false
    )
    {
        if (divisionCode <= 0)
            return BadRequest("Error: Missing or invalid required field 'divisionCode'.");

        string result = await scraperService.ScrapeDivision(divisionCode, overrideCache);
        return Content(result, "application/json");
    }
}
