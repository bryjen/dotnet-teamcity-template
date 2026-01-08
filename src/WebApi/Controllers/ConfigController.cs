using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WebApi.Configuration;

namespace WebApi.Controllers;

/// <summary>
/// Debug endpoint to show configuration values (for troubleshooting)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ConfigController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Shows the CORS configuration that's actually being used
    /// </summary>
    [HttpGet("cors")]
    public IActionResult GetCorsConfig()
    {
        // Use the exact same logic as Program.cs
        var resolvedOrigins = ServiceConfiguration.GetCorsAllowedOrigins(_configuration);

        // Check all possible configuration keys for debugging
        var corsOriginsString = _configuration["Cors:AllowedOrigins:0"] 
                             ?? _configuration["Cors__AllowedOrigins__0"]
                             ?? _configuration["Cors:AllowedOrigins__0"]
                             ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0");

        var corsOriginsArray = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        // Build response showing what we found
        var response = new
        {
            corsOriginsString = corsOriginsString ?? "(null)",
            corsOriginsArray = corsOriginsArray ?? Array.Empty<string>(),
            corsOriginsArrayLength = corsOriginsArray?.Length ?? 0,
            environmentVariable = Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0") ?? "(not set)",
            allConfigKeys = new
            {
                corsAllowedOrigins0 = _configuration["Cors:AllowedOrigins:0"] ?? "(null)",
                corsAllowedOriginsUnderscore0 = _configuration["Cors__AllowedOrigins__0"] ?? "(null)",
                corsAllowedOriginsHybrid = _configuration["Cors:AllowedOrigins__0"] ?? "(null)",
            },
            resolvedOrigins = resolvedOrigins,
            resolvedOriginsCount = resolvedOrigins.Length,
            willAllowAllOrigins = resolvedOrigins.Length == 0
        };

        return Ok(response);
    }
}

