using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebApi.Data;

namespace WebApi.Configuration;

public static class ServiceConfiguration
{
    /// <summary>
    /// Resolves CORS allowed origins from configuration.
    /// If environment variable is set, use it (comma-separated). Otherwise fall back to appsettings.json array.
    /// </summary>
    public static string[] GetCorsAllowedOrigins(IConfiguration configuration)
    {
        // Check environment variable first
        var corsOriginsString = configuration["Cors__AllowedOrigins"]
                              ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins");

        if (!string.IsNullOrWhiteSpace(corsOriginsString))
        {
            return corsOriginsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Fall back to appsettings.json array
        var corsOriginsArray = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        return corsOriginsArray ?? Array.Empty<string>();
    }
    public static void ConfigureDatabase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment = null)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger("WebApi.Configuration.ServiceConfiguration");

        // Skip SQL Server registration if running in test mode (tests swap in an in-memory DbContext).
        // Important: don't require a connection string in Test env, otherwise WebApplicationFactory can fail
        // before test configuration overrides are applied.
        var isTestEnvironment = environment?.IsEnvironment("Test") ?? false;
        if (isTestEnvironment)
        {
            logger.LogInformation("Environment 'Test' detected. Skipping database registration (tests will configure DbContext separately).");
            return;
        }

        // Temporary/global switch: if Database:Provider is set to InMemory, always use in-memory DB
        var configuredProvider = configuration["Database:Provider"];
        if (string.Equals(configuredProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Configuration 'Database:Provider=InMemory' detected. Using in-memory database provider 'FallbackInMemoryDatabase' and skipping SQL Server.");
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
            return;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Also treat the sentinel value "InMemory" as test mode (used by tests / local tooling).
        if (string.Equals(connectionString, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Connection string 'DefaultConnection' is set to 'InMemory'. Skipping SQL Server registration (tests/local tooling).");
            return;
        }

        // If no connection string is provided, use in-memory database
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Connection string 'DefaultConnection' not found. Falling back to in-memory database.");
            logger.LogInformation("Using in-memory database provider 'FallbackInMemoryDatabase'.");

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
            return;
        }

        // Try to test the connection, fall back to in-memory if it fails
        try
        {
            // Extract server IP from connection string for logging
            var serverMatch = System.Text.RegularExpressions.Regex.Match(connectionString, @"Server=([^,;]+)");
            var serverInfo = serverMatch.Success ? serverMatch.Groups[1].Value : "unknown";
            logger.LogInformation("Attempting to connect to SQL Server at: {ServerInfo}", serverInfo);
            
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString, options => 
                {
                    options.CommandTimeout(30); // 30 second timeout for connection test (allows time for Tailscale routing)
                });
            
            using var testContext = new AppDbContext(optionsBuilder.Options);
            
            // Test the connection (synchronous to avoid deadlock in configuration phase)
            logger.LogInformation("Testing database connection...");
            
            // Try to actually open a connection to get detailed error information
            try
            {
                var canConnect = testContext.Database.CanConnect();
                
                if (canConnect)
                {
                    // Connection successful, use SQL Server
                    logger.LogInformation("Successfully connected to SQL Server using connection string 'DefaultConnection'. Using SQL Server database provider.");
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlServer(connectionString));
                    return;
                }
                
                // CanConnect returned false - try to get more info by attempting to execute a query
                logger.LogWarning("CanConnect() returned false. Attempting to get more details...");
                try
                {
                    // Force an actual connection attempt - this will throw an exception with details
                    _ = testContext.Database.ExecuteSqlRaw("SELECT 1");
                    logger.LogWarning("ExecuteSqlRaw succeeded even though CanConnect() returned false - this is unusual");
                }
                catch (Microsoft.Data.SqlClient.SqlException sqlQueryEx)
                {
                    logger.LogError(sqlQueryEx, 
                        "SQL Server connection failed during test query. Error Number: {Number}, State: {State}, Class: {Class}, Server: {Server}, Message: {Message}",
                        sqlQueryEx.Number, sqlQueryEx.State, sqlQueryEx.Class, sqlQueryEx.Server, sqlQueryEx.Message);
                    throw; // Re-throw to be caught by outer catch
                }
                catch (Exception queryEx)
                {
                    logger.LogError(queryEx, "Failed to execute test query. Exception Type: {ExceptionType}, Message: {ErrorMessage}, StackTrace: {StackTrace}", 
                        queryEx.GetType().Name, queryEx.Message, queryEx.StackTrace);
                    throw; // Re-throw to be caught by outer catch
                }
                
                logger.LogWarning("SQL Server connection test for 'DefaultConnection' returned false (CanConnect() = false). Falling back to in-memory database.");
                logger.LogWarning("This usually means the database server is unreachable, not accepting connections, or authentication failed.");
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                // SQL Server specific exception
                logger.LogError(sqlEx, 
                    "SQL Server connection failed. Error Number: {Number}, State: {State}, Class: {Class}, Server: {Server}, Message: {Message}", 
                    sqlEx.Number, sqlEx.State, sqlEx.Class, sqlEx.Server, sqlEx.Message);
                throw; // Re-throw to be caught by outer catch
            }
        }
        catch (Exception ex)
        {
            // Connection failed, will fall back to in-memory below
            logger.LogError(ex, 
                "Failed to connect to SQL Server database using connection string. " +
                "Exception Type: {ExceptionType}, Message: {ErrorMessage}, StackTrace: {StackTrace}. Falling back to in-memory database.", 
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace);
            
            // Log inner exception if present
            if (ex.InnerException != null)
            {
                logger.LogError("Inner exception: {InnerExceptionType} - {InnerExceptionMessage}", 
                    ex.InnerException.GetType().Name, 
                    ex.InnerException.Message);
            }
        }

        // Fall back to in-memory database
        logger.LogInformation("Using in-memory database provider 'FallbackInMemoryDatabase' due to SQL Server connection issues.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
    }
}

