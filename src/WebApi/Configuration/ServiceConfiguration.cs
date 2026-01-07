using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebApi.Data;

namespace WebApi.Configuration;

public static class ServiceConfiguration
{
    public static void ConfigureDatabase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment = null)
    {
        // Skip SQL Server registration if running in test mode (tests swap in an in-memory DbContext).
        // Important: don't require a connection string in Test env, otherwise WebApplicationFactory can fail
        // before test configuration overrides are applied.
        var isTestEnvironment = environment?.IsEnvironment("Test") ?? false;
        if (isTestEnvironment)
        {
            return;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Also treat the sentinel value "InMemory" as test mode (used by tests / local tooling).
        if (string.Equals(connectionString, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // If no connection string is provided, use in-memory database
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger("WebApi.Configuration.ServiceConfiguration");
            logger.LogError("Connection string 'DefaultConnection' not found. Falling back to in-memory database.");
            
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
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(connectionString));
                return;
            }
        }
        catch (Exception ex)
        {
            // Connection failed, will fall back to in-memory below
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger("WebApi.Configuration.ServiceConfiguration");
            logger.LogError(ex, 
                "Failed to connect to SQL Server database using connection string. " +
                "Error: {ErrorMessage}. Falling back to in-memory database.", 
                ex.Message);
        }

        // Fall back to in-memory database
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
    }
}

