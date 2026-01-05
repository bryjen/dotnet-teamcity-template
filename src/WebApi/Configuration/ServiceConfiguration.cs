using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WebApi.Data;

namespace WebApi.Configuration;

public static class ServiceConfiguration
{
    public static void ConfigureDatabase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment = null)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
        
        // Skip SQL Server registration if running in test mode
        var isTestMode = environment?.IsEnvironment("Test") ?? connectionString == "InMemory";
        
        if (!isTestMode)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));
        }
    }
}

