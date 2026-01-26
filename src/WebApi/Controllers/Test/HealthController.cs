using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;

namespace WebApi.Controllers.Test;

/// <summary>
/// Health check endpoint for monitoring service status
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class HealthController(
    AppDbContext context, 
    ILogger<HealthController> logger) 
    : ControllerBase
{
    /// <summary>
    /// Get detailed health status of the API and its dependencies
    /// </summary>
    /// <returns>Health status information including database connectivity</returns>
    /// <response code="200">Service is healthy</response>
    /// <response code="503">Service is unhealthy</response>
    /// <remarks>
    /// Provides comprehensive health check information for the API service and its dependencies (database, etc.).
    /// Useful for monitoring, load balancers, and container orchestration systems.
    /// 
    /// **Example Request:**
    /// ```
    /// GET /api/v1/health
    /// ```
    /// 
    /// **Example Response (200 OK - Healthy):**
    /// ```json
    /// {
    ///   "status": "Healthy",
    ///   "timestamp": "2024-01-15T10:00:00Z",
    ///   "version": "1.0.0.0",
    ///   "database": {
    ///     "status": "Connected",
    ///     "provider": "Npgsql.EntityFrameworkCore.PostgreSQL"
    ///   }
    /// }
    /// ```
    /// 
    /// **Example Response (200 OK - In-Memory Database):**
    /// ```json
    /// {
    ///   "status": "Healthy",
    ///   "timestamp": "2024-01-15T10:00:00Z",
    ///   "version": "1.0.0.0",
    ///   "database": {
    ///     "status": "InMemory",
    ///     "provider": "InMemory"
    ///   }
    /// }
    /// ```
    /// 
    /// **Example Response (503 Service Unavailable - Database Error):**
    /// ```json
    /// {
    ///   "status": "Unhealthy",
    ///   "timestamp": "2024-01-15T10:00:00Z",
    ///   "version": "1.0.0.0",
    ///   "database": {
    ///     "status": "Error",
    ///     "provider": "Npgsql.EntityFrameworkCore.PostgreSQL",
    ///     "error": "Connection timeout"
    ///   }
    /// }
    /// ```
    /// 
    /// **Database Status Values:**
    /// - `Connected`: Database is reachable and responding
    /// - `Disconnected`: Database connection failed
    /// - `InMemory`: Using in-memory database (development/testing)
    /// - `Error`: Database check failed with an error
    /// 
    /// **Usage Notes:**
    /// - Use this endpoint for health monitoring and automated checks
    /// - Load balancers can use this to determine if the service should receive traffic
    /// - Container orchestration systems (Kubernetes, Docker Swarm) can use this for liveness/readiness probes
    /// - The endpoint checks database connectivity but does not perform heavy operations
    /// - Response time should be minimal for monitoring purposes
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatus>> GetHealth()
    {
        var health = new HealthStatus
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown"
        };

        // Check database connectivity
        try
        {
            if (context.Database.IsRelational())
            {
                var canConnect = await context.Database.CanConnectAsync();
                health.Database = new DatabaseHealth
                {
                    Status = canConnect ? "Connected" : "Disconnected",
                    Provider = context.Database.ProviderName ?? "Unknown"
                };
            }
            else
            {
                health.Database = new DatabaseHealth
                {
                    Status = "InMemory",
                    Provider = "InMemory"
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            health.Database = new DatabaseHealth
            {
                Status = "Error",
                Provider = context.Database.ProviderName ?? "Unknown",
                Error = ex.Message
            };
            health.Status = "Unhealthy";
        }

        // Determine overall status
        if (health.Status == "Unhealthy" || 
            (health.Database != null && health.Database.Status == "Error"))
        {
            return StatusCode(503, health);
        }

        return Ok(health);
    }
}

/// <summary>
/// Health status response model
/// </summary>
public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public DatabaseHealth? Database { get; set; }
}

/// <summary>
/// Database health information
/// </summary>
public class DatabaseHealth
{
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Error { get; set; }
}
