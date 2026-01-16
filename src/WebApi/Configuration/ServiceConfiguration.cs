using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Resend;
using WebApi.Configuration.Options;
using WebApi.Data;
using WebApi.Services.Auth;
using WebApi.Services.Email;
using WebApi.Services.Validation;

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
            options.SupportNonNullableReferenceTypes();
            
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
            // Read retry policy configuration
            var retryPolicyConfig = configuration.GetSection(DatabaseRetryPolicySettings.SectionName);
            var maxRetryCount = retryPolicyConfig.GetValue<int>("MaxRetryCount", 5);
            var maxRetryDelaySeconds = retryPolicyConfig.GetValue<int>("MaxRetryDelaySeconds", 30);
            
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, npgsqlOptions =>
                {
                    // Enable retry on failure for transient database errors
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: maxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                        errorCodesToAdd: null); // Uses default PostgreSQL transient error codes
                });
            using var testContext = new AppDbContext(optionsBuilder.Options);
            var canConnect = testContext.Database.CanConnect();
            if (canConnect)
            {
                // Connection successful, use PostgreSQL with retry policy
                logger.LogInformation("Successfully connected to PostgreSQL using connection string 'DefaultConnection'. Using PostgreSQL database provider with retry policy (MaxRetryCount: {MaxRetryCount}, MaxRetryDelay: {MaxRetryDelay}s).", 
                    maxRetryCount, maxRetryDelaySeconds);
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        // Enable retry on failure for transient database errors
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: maxRetryCount,
                            maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                            errorCodesToAdd: null); // Uses default PostgreSQL transient error codes
                    }));
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
    
    /// <summary>
    /// Configures security headers for the application.
    /// </summary>
    /// <remarks>
    /// Security headers are applied via SecurityHeadersMiddleware.
    /// This method is kept for consistency with other configuration methods,
    /// but the actual implementation is in the middleware.
    /// </remarks>
    public static void ConfigureSecurityHeaders(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Security headers are applied via SecurityHeadersMiddleware
        // No service registration needed for the custom middleware
    }
    
    /// <summary>
    /// Configures rate limiting for the application.
    /// </summary>
    /// <remarks>
    /// Sets up three rate limiting policies:
    /// - Global: Applies to all requests (100 requests per minute per IP/user)
    /// - Auth: Stricter limits for authentication endpoints (5 requests per minute per IP)
    /// - Authenticated: Higher limits for authenticated users (200 requests per minute per user)
    /// </remarks>
    public static void ConfigureRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var globalConfig = configuration.GetSection("RateLimiting:Global");
        var authConfig = configuration.GetSection("RateLimiting:Auth");
        var authenticatedConfig = configuration.GetSection("RateLimiting:Authenticated");
        
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            // Global rate limiter - applies to all requests
            var globalPermitLimit = globalConfig.GetValue<int>("PermitLimit", 100);
            var globalWindowMinutes = globalConfig.GetValue<int>("WindowMinutes", 1);
            var globalQueueLimit = globalConfig.GetValue<int>("QueueLimit", 10);
            
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Use authenticated user ID if available, otherwise use IP address
                var partitionKey = context.User.Identity?.Name 
                    ?? context.Connection.RemoteIpAddress?.ToString() 
                    ?? "anonymous";
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = globalPermitLimit,
                        Window = TimeSpan.FromMinutes(globalWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = globalQueueLimit
                    });
            });
            
            // Auth endpoints policy - stricter limits to prevent brute force attacks (IP-based)
            var authPermitLimit = authConfig.GetValue<int>("PermitLimit", 5);
            var authWindowMinutes = authConfig.GetValue<int>("WindowMinutes", 1);
            var authQueueLimit = authConfig.GetValue<int>("QueueLimit", 2);
            
            options.AddPolicy("auth", context =>
            {
                var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = authPermitLimit,
                        Window = TimeSpan.FromMinutes(authWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = authQueueLimit
                    });
            });
            
            // Authenticated users policy - higher limits for authenticated users
            var authenticatedPermitLimit = authenticatedConfig.GetValue<int>("PermitLimit", 200);
            var authenticatedWindowMinutes = authenticatedConfig.GetValue<int>("WindowMinutes", 1);
            var authenticatedQueueLimit = authenticatedConfig.GetValue<int>("QueueLimit", 20);
            
            options.AddPolicy("authenticated", context =>
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    // If not authenticated, use no limiter (will fall back to global)
                    return RateLimitPartition.GetNoLimiter("anonymous");
                }
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = authenticatedPermitLimit,
                        Window = TimeSpan.FromMinutes(authenticatedWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = authenticatedQueueLimit
                    });
            });
            
            // Custom response when rate limit is exceeded
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                
                // Try to get retry after from metadata
                var retryAfter = 60; // default to 60 seconds
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
                {
                    retryAfter = (int)((TimeSpan)retryAfterValue).TotalSeconds;
                }
                
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();
                
                var response = new
                {
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfter = retryAfter
                };
                
                await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            };
        });
    }
    
    /// <summary>
    /// Configures response compression for the application (production only).
    /// </summary>
    /// <remarks>
    /// Enables Gzip and Brotli compression for responses to reduce bandwidth usage.
    /// Only enabled in production environment.
    /// </remarks>
    public static void ConfigureResponseCompression(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        // Only enable compression in production
        if (!environment.IsProduction())
        {
            return;
        }

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true; // Enable compression for HTTPS
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            
            // Compress these MIME types
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "application/xml",
                "text/json",
                "text/xml"
            });
        });

        // Configure compression levels
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });
    }

    /// <summary>
    /// Configures response caching for the application (production only).
    /// </summary>
    /// <remarks>
    /// Enables HTTP-level response caching to reduce server load and improve performance.
    /// Only enabled in production environment.
    /// </remarks>
    public static void ConfigureResponseCaching(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        // Only enable response caching in production
        if (!environment.IsProduction())
        {
            return;
        }

        services.AddResponseCaching(options =>
        {
            // Maximum cacheable response size (100 MB)
            options.MaximumBodySize = 100 * 1024 * 1024;
            
            // Maximum cache size (100 MB)
            options.SizeLimit = 100 * 1024 * 1024;
            
            // Use case-sensitive paths for cache keys
            options.UseCaseSensitivePaths = false;
        });
    }
    
    /// <summary>
    /// Configures request size limits for the application.
    /// </summary>
    /// <remarks>
    /// Sets global request body size limits and form data limits.
    /// Individual endpoints can override these limits using the [RequestSizeLimit(bytes)] attribute.
    /// </remarks>
    public static void ConfigureRequestLimits(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var limitsConfig = configuration.GetSection(RequestLimitsSettings.SectionName);
        var maxRequestBodySizeBytes = limitsConfig.GetValue<long>("MaxRequestBodySizeBytes", 10 * 1024 * 1024); // Default: 10 MB
        var maxFormValueLength = limitsConfig.GetValue<int>("MaxFormValueLength", 4 * 1024 * 1024); // Default: 4 MB
        var maxFormKeyLength = limitsConfig.GetValue<int>("MaxFormKeyLength", 2 * 1024); // Default: 2 KB
        var maxFormFileSizeBytes = limitsConfig.GetValue<long>("MaxFormFileSizeBytes", 5 * 1024 * 1024); // Default: 5 MB

        // Configure form options (for form data limits)
        services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = maxFormValueLength;
            options.KeyLengthLimit = maxFormKeyLength;
            options.MultipartBodyLengthLimit = maxRequestBodySizeBytes;
            options.MultipartHeadersLengthLimit = 16384; // 16 KB for headers
        });

        // Configure Kestrel server options (for request body size limits)
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = maxRequestBodySizeBytes;
        });

        // Configure IIS server options (for IIS hosting)
        services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = maxRequestBodySizeBytes;
        });
    }
    
    /// <summary>
    /// Configures authentication-related services.
    /// </summary>
    public static void ConfigureAuthServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient(); // Required for GitHubTokenValidationService
        services.AddScoped<JwtTokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<PasswordValidator>();
        services.AddScoped<GoogleTokenValidationService>();
        services.AddScoped<MicrosoftTokenValidationService>();
        services.AddScoped<GitHubTokenValidationService>();
        services.AddScoped<TokenValidationServiceFactory>();
        services.AddScoped<AuthService>();
        
        // Configure PasswordResetService
        services.AddScoped<PasswordResetService>(sp =>
        {
            var frontendUrl = configuration["Frontend:BaseUrl"] ?? throw new InvalidOperationException("Frontend URL not configured");
            return new PasswordResetService(
                sp.GetRequiredService<AppDbContext>(), 
                sp.GetRequiredService<IEmailService>(),
                sp.GetRequiredService<PasswordValidator>(),
                frontendUrl);
        });
    }
    
    internal static void ConfigureJsonCallback(JsonOptions options)
    {
        var jsonSerializerOptions = options.JsonSerializerOptions;
        jsonSerializerOptions.WriteIndented = true;
        jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        jsonSerializerOptions.PropertyNameCaseInsensitive = true;
    }
}