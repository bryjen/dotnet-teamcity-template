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
        var corsOriginsString = _configuration["Cors__AllowedOrigins"]
                             ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins")
                             ?? _configuration["Cors:AllowedOrigins:0"] 
                             ?? _configuration["Cors__AllowedOrigins__0"]
                             ?? _configuration["Cors:AllowedOrigins__0"]
                             ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0");

        var corsOriginsArray = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

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
            environmentVariableWithIndex = Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0") ?? "(not set)",
            allCorsEnvironmentVariables = allCorsEnvVars,
            allConfigKeys = new
            {
                corsAllowedOrigins = _configuration["Cors__AllowedOrigins"] ?? "(null)",
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

