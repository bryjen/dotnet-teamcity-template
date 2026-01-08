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

        // Skip PostgreSQL registration if running in test mode (tests swap in an in-memory DbContext).
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
            logger.LogInformation("Configuration 'Database:Provider=InMemory' detected. Using in-memory database provider 'FallbackInMemoryDatabase' and skipping PostgreSQL.");
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
            return;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Also treat the sentinel value "InMemory" as test mode (used by tests / local tooling).
        if (string.Equals(connectionString, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Connection string 'DefaultConnection' is set to 'InMemory'. Skipping PostgreSQL registration (tests/local tooling).");
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
                .UseNpgsql(connectionString);
            
            using var testContext = new AppDbContext(optionsBuilder.Options);
            
            // Test the connection (synchronous to avoid deadlock in configuration phase)
            var canConnect = testContext.Database.CanConnect();
            
            if (canConnect)
            {
                // Connection successful, use PostgreSQL
                logger.LogInformation("Successfully connected to PostgreSQL using connection string 'DefaultConnection'. Using PostgreSQL database provider.");
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(connectionString));
                return;
            }
            
            logger.LogWarning("PostgreSQL connection test for 'DefaultConnection' returned false. Falling back to in-memory database.");
        }
        catch (Exception ex)
        {
            // Connection failed, will fall back to in-memory below
            logger.LogError(ex, 
                "Failed to connect to PostgreSQL database using connection string. " +
                "Error: {ErrorMessage}. Falling back to in-memory database.", 
                ex.Message);
        }

        // Fall back to in-memory database
        logger.LogInformation("Using in-memory database provider 'FallbackInMemoryDatabase' due to PostgreSQL connection issues.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
    }
}

