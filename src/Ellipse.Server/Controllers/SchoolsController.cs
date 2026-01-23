using Ellipse.Server.Services;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Serilog;

namespace Ellipse.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
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
            return BadRequest("Error: Missing or invalid field 'divisionCode'.");

        string result = await scraperService.ScrapeDivision(divisionCode, overrideCache);
        if (string.IsNullOrEmpty(result))
            return StatusCode(StatusCodes.Status500InternalServerError);

        return Content(result, "application/json");
    }
}