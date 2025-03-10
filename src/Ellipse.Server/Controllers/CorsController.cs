using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace Ellipse.Server.Controllers;

[Route("api/[controller]")]
public sealed class CORSController(IOptionsMonitor<CorsOptions> corsOptions) : ControllerBase
{
    private readonly IOptionsMonitor<CorsOptions> _corsOptions = corsOptions;

    [HttpPost("add-origin")]
    public IActionResult AddOrigin([FromBody] string origin)
    {
        var corsPolicy = _corsOptions.CurrentValue.GetPolicy("DynamicCors");
        if (!corsPolicy.Origins.Contains(origin))
        {
            corsPolicy.Origins.Add(origin);
        }

        return Ok();
    }
}