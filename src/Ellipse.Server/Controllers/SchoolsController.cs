using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Ellipse.Server.Controllers;


[ApiController]
[Route("api/[controller]")]
[RequestTimeout("ResponseTimeout")]
public class SchoolsController : ControllerBase
{
    // POST api/schools/get-schools
    [HttpPost("get-schools")]
    public async Task<IActionResult> GetSchools([FromForm] int divisionCode, [FromForm] bool overrideCache = false)
    {
        if (divisionCode <= 0)
        {
            return BadRequest("Error: Missing or invalid required field 'divisionCode'.");
        }

        var result = await WebScraper.StartNewAsync(divisionCode, overrideCache);
        return Content(result, "application/json");
    }
}

