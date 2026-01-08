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
    /// Supports both array format (appsettings.json) and single string/comma-separated (env vars).
    /// Terraform sets Cors__AllowedOrigins__0 as a string, so read it as string first, then split.
    /// </summary>
    public static string[] GetCorsAllowedOrigins(IConfiguration configuration)
    {
        // Check multiple possible key formats to ensure we catch the env var
        var corsOriginsString = configuration["Cors:AllowedOrigins:0"] 
                              ?? configuration["Cors__AllowedOrigins__0"]
                              ?? configuration["Cors:AllowedOrigins__0"]
                              ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0");

        if (!string.IsNullOrWhiteSpace(corsOriginsString))
        {
            // Environment variable or single string value - split by comma if needed
            // Handles both "origin1" and "origin1,origin2,origin3"
            return corsOriginsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Try array format from appsettings.json: ["origin1", "origin2"]
        var corsOriginsArray = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (corsOriginsArray != null && corsOriginsArray.Length > 0)
        {
            return corsOriginsArray;
        }

        // Empty = allow all origins (permissive mode)
        return Array.Empty<string>();
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
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString);
            
            using var testContext = new AppDbContext(optionsBuilder.Options);
            
            // Test the connection (synchronous to avoid deadlock in configuration phase)
            var canConnect = testContext.Database.CanConnect();
            
            if (canConnect)
            {
                // Connection successful, use SQL Server
                logger.LogInformation("Successfully connected to SQL Server using connection string 'DefaultConnection'. Using SQL Server database provider.");
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(connectionString));
                return;
            }
            
            logger.LogWarning("SQL Server connection test for 'DefaultConnection' returned false. Falling back to in-memory database.");
        }
        catch (Exception ex)
        {
            // Connection failed, will fall back to in-memory below
            logger.LogError(ex, 
                "Failed to connect to SQL Server database using connection string. " +
                "Error: {ErrorMessage}. Falling back to in-memory database.", 
                ex.Message);
        }

        // Fall back to in-memory database
        logger.LogInformation("Using in-memory database provider 'FallbackInMemoryDatabase' due to SQL Server connection issues.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
    }
}

