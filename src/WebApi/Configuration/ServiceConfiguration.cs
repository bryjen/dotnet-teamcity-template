using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Resend;
using WebApi.Data;
using WebApi.Services.Email;

namespace WebApi.Configuration;

public static class ServiceConfiguration
{
    /// <summary>
    /// Resolves CORS allowed origins from configuration.
    /// If an environment variable is set, use it (comma-separated). Otherwise, fall back to `appsettings.json` array.
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
    
    public static void ConfigureOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });
    }
    
    /// <summary>
    /// Configures the application's database provider.
    /// </summary>
    /// <remarks>
    /// Falls back to an in-memory EF Core configuration in the case of an empty connection string, OR if connecting
    /// to the specified URL fails. 
    /// </remarks>
    public static void ConfigureDatabase(
        this IServiceCollection services, 
        IConfiguration configuration, 
        IHostEnvironment? environment = null)
    {
        // Create a temporary logger factory for configuration logging
        // This is acceptable since it's only used during service configuration
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
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

        // temporary/global switch: if Database:Provider is set to InMemory, always use in-memory DB
        var configuredProvider = configuration["Database:Provider"];
        if (string.Equals(configuredProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Configuration 'Database:Provider=InMemory' detected. Using in-memory database provider 'FallbackInMemoryDatabase' and skipping PostgreSQL.");
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
            return;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Connection string 'DefaultConnection' not found. Falling back to in-memory database.");
            logger.LogInformation("Using in-memory database provider 'FallbackInMemoryDatabase'.");

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
            return;
        }
        else if (string.Equals(connectionString, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Connection string 'DefaultConnection' is set to 'InMemory'. Skipping PostgreSQL registration (tests/local tooling).");
            return;
        }

        
        // Set the intended provider, + we try testing the connection, falling back to in-memory if it fails.
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString);
            using var testContext = new AppDbContext(optionsBuilder.Options);
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
            logger.LogError(ex, 
                "Failed to connect to PostgreSQL database using connection string. " +
                "Error: {ErrorMessage}. Falling back to in-memory database.", 
                ex.Message);
        }

        // in case of no early exit, fall back to an in memory database
        logger.LogInformation("Using in-memory database provider 'FallbackInMemoryDatabase' due to PostgreSQL connection issues.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("FallbackInMemoryDatabase"));
    }
    
    public static void ConfigureCors(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var corsOrigins = ServiceConfiguration.GetCorsAllowedOrigins(configuration);
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (corsOrigins.Length == 0)
                {
                    // Allow all origins (permissive mode) - cannot use AllowCredentials() with AllowAnyOrigin()
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
                else
                {
                    // Specific origins - can use credentials
                    policy.WithOrigins(corsOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });
        });
    }
    
    public static void ConfigureJwtAuth(
        this IServiceCollection services, 
        IConfiguration configuration,
        IHostEnvironment environment)
    { 
        // Add JWT Authentication
        var jwtSettings = configuration.GetSection("Jwt");

        // Tests (WebApplicationFactory) can run before test-specific configuration overrides are applied.
        // Provide a safe default JWT secret in Test environment so the host can start deterministically.
        if (environment.IsEnvironment("Test") && string.IsNullOrWhiteSpace(jwtSettings["Secret"]))
        {
            configuration["Jwt:Secret"] = "TestJwtSecret_ForLocalUnitTests_ChangeMe_1234567890";
            jwtSettings = configuration.GetSection("Jwt");
        }
        var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
    }
    
    public static void ConfigureOpenTelemetry(
        this IServiceCollection services, 
        IConfiguration configuration,
        ILoggingBuilder loggingBuilder,
        IHostEnvironment environment)
    {
        // only emit otel metrics if we aren't in test
        if (environment.IsEnvironment("Test")) 
            return;
        
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? "WebApi";
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);

        Uri? endpointUri = null;
        if (!string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var parsed))
        {
            endpointUri = parsed;
        }

        // Logs (structured)
        loggingBuilder.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;

            if (endpointUri != null)
            {
                logging.AddOtlpExporter(o => o.Endpoint = endpointUri);
            }
        });

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (endpointUri != null)
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = endpointUri);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (endpointUri != null)
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = endpointUri);
                }
            });
    }
    
    
    public static void ConfigureEmail(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var resendApiKey = configuration["Email:Resend:ApiKey"] ?? string.Empty;
        var emailDomain = configuration["Email:Resend:Domain"] ?? string.Empty;
        services.AddSingleton<IResend>(ResendClient.Create(resendApiKey));
        services.AddTransient<IEmailService, RenderMjmlEmailService>(sp =>
            new RenderMjmlEmailService(sp.GetRequiredService<IResend>(), emailDomain));
    }
    
    internal static void ConfigureJsonCallback(JsonOptions options)
    {
        var jsonSerializerOptions = options.JsonSerializerOptions;
        jsonSerializerOptions.WriteIndented = true;
        jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        jsonSerializerOptions.PropertyNameCaseInsensitive = true;
    }
}