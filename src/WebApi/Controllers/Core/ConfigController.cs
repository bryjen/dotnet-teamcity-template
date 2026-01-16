using Microsoft.AspNetCore.Mvc;
using WebApi.Configuration;

namespace WebApi.Controllers.Core;

/// <summary>
/// Debug endpoint to show configuration values (for troubleshooting)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ConfigController(
    IConfiguration configuration) 
    : ControllerBase
{
    /// <summary>
    /// Shows the CORS configuration that's actually being used
    /// </summary>
    [HttpGet("cors")]
    public IActionResult GetCorsConfig()
    {
        // Use the exact same logic as Program.cs
        var resolvedOrigins = ServiceConfiguration.GetCorsAllowedOrigins(configuration);

        // Check configuration key for debugging
        var corsOriginsString = configuration["Cors__AllowedOrigins"]
                             ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins");

        var corsOriginsArray = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        // Get all environment variables that start with "Cors" for debugging
        var allCorsEnvVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(e => e.Key.ToString()?.StartsWith("Cors", StringComparison.OrdinalIgnoreCase) == true)
            .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? "(null)");

        // Build response showing what we found
        var response = new
        {
            corsOriginsString = corsOriginsString ?? "(null)",
            corsOriginsArray = corsOriginsArray ?? Array.Empty<string>(),
            corsOriginsArrayLength = corsOriginsArray?.Length ?? 0,
            environmentVariable = Environment.GetEnvironmentVariable("Cors__AllowedOrigins") ?? "(not set)",
            allCorsEnvironmentVariables = allCorsEnvVars,
            allConfigKeys = new
            {
                corsAllowedOrigins = configuration["Cors__AllowedOrigins"] ?? "(null)",
            },
            resolvedOrigins = resolvedOrigins,
            resolvedOriginsCount = resolvedOrigins.Length,
            willAllowAllOrigins = resolvedOrigins.Length == 0
        };

        return Ok(response);
    }
}

