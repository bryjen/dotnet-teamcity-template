using Microsoft.EntityFrameworkCore;
using WebApi.Data;

namespace WebApi.Configuration;

public static class ServiceConfiguration
{
    public static void ConfigureDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
        
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));
    }
}

