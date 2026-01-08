using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using WebApi.Configuration;
using WebApi.Data;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Include XML comments for enhanced documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add DbContext
builder.Services.ConfigureDatabase(builder.Configuration, builder.Environment);

// Add CORS
// Support both array format (appsettings.json) and single string/comma-separated (env vars)
// Terraform sets Cors__AllowedOrigins__0 which maps to Cors:AllowedOrigins[0]
var corsOriginsArray = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
var corsOriginsSingle = builder.Configuration["Cors:AllowedOrigins:0"] ?? builder.Configuration["Cors__AllowedOrigins__0"];

string[] corsOrigins;
if (corsOriginsArray != null && corsOriginsArray.Length > 0)
{
    // Array format from appsettings.json: ["origin1", "origin2"]
    corsOrigins = corsOriginsArray;
}
else if (!string.IsNullOrWhiteSpace(corsOriginsSingle))
{
    // Single value or comma-separated from environment variable
    // Handle both "origin1" and "origin1,origin2,origin3"
    corsOrigins = corsOriginsSingle.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
else
{
    // Empty = allow all origins (permissive mode)
    corsOrigins = Array.Empty<string>();
}

builder.Services.AddCors(options =>
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

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Tests (WebApplicationFactory) can run before test-specific configuration overrides are applied.
// Provide a safe default JWT secret in Test environment so the host can start deterministically.
if (builder.Environment.IsEnvironment("Test") && string.IsNullOrWhiteSpace(jwtSettings["Secret"]))
{
    builder.Configuration["Jwt:Secret"] = "TestJwtSecret_ForLocalUnitTests_ChangeMe_1234567890";
    jwtSettings = builder.Configuration.GetSection("Jwt");
}
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

builder.Services.AddAuthentication(options =>
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

builder.Services.AddAuthorization();

// OpenTelemetry (traces) - export via OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set.
// This keeps observability optional and avoids test/network coupling by default.
if (!builder.Environment.IsEnvironment("Test"))
{
    var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "WebApi";
    var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName);

    Uri? endpointUri = null;
    if (!string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var parsed))
    {
        endpointUri = parsed;
    }

    // Logs (structured)
    builder.Logging.AddOpenTelemetry(logging =>
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

    builder.Services.AddOpenTelemetry()
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

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ITagService, TagService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// no need to hide openapi docs since this is a "test" project anyways
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo App API v1");
    options.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    options.DocumentTitle = "Todo App API Documentation";
    options.DefaultModelsExpandDepth(2);
    options.DefaultModelExpandDepth(2);
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    options.EnableDeepLinking();
    options.DisplayRequestDuration();
});

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply migrations automatically in container/dev environments (safe no-op if already applied).
// This is required for docker-compose scenarios where the DB starts empty.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var env = services.GetRequiredService<IHostEnvironment>();

    // Only attempt migration if a relational DbContext is registered (tests override this).
    if (!env.IsEnvironment("Test"))
    {
        var db = services.GetService<AppDbContext>();
        if (db != null && db.Database.IsRelational())
        {
            db.Database.Migrate();
        }
    }
}

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }