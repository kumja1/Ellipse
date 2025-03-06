using Microsoft.AspNetCore.Mvc;

namespace Ellipse.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchoolsController : ControllerBase
{
    // POST api/schools/get-schools
    [HttpPost("get-schools")]
    public async Task<IActionResult> GetSchools([FromForm] int divisionCode)
    {
        if (divisionCode <= 0)
        {
            return BadRequest("Error: Missing or invalid required field 'divisionCode'.");
        }

        var result = await WebScraper.StartNewAsync(divisionCode);
        return Content(result, "application/json");
    }
}

