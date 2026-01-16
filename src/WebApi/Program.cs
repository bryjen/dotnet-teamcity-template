using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApi.Configuration;
using WebApi.Configuration.Options;
using WebApi.Configuration.Validators;
using WebApi.Data;
using WebApi.Middleware;
using WebApi.Services.Auth;
using WebApi.Services.Email;
using WebApi.Services.Tag;
using WebApi.Services.Todo;
using WebApi.Services.Validation;
using WebApi.Validators;

var builder = WebApplication.CreateBuilder(args);

// Bind and validate configuration options
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddSingleton<IValidateOptions<EmailSettings>, EmailSettingsValidator>();

builder.Services.Configure<FrontendSettings>(
    builder.Configuration.GetSection(FrontendSettings.SectionName));
builder.Services.AddSingleton<IValidateOptions<FrontendSettings>, FrontendSettingsValidator>();

builder.Services.Configure<RateLimitingSettings>(
    builder.Configuration.GetSection(RateLimitingSettings.SectionName));
builder.Services.AddSingleton<IValidateOptions<RateLimitingSettings>, RateLimitingSettingsValidator>();

builder.Services.Configure<OAuthSettings>(
    builder.Configuration.GetSection(OAuthSettings.SectionName));

builder.Services
    .AddControllers()
    .AddJsonOptions(ServiceConfiguration.ConfigureJsonCallback);

// FluentValidation configuration
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

builder.Services.ConfigureEmail(builder.Configuration);

builder.Services.ConfigureOpenApi();
builder.Services.ConfigureDatabase(builder.Configuration, builder.Environment);
builder.Services.ConfigureCors(builder.Configuration);
// Configure Data Protection for cookie encryption (required for OAuth state)
builder.Services.AddDataProtection();
builder.Services.ConfigureJwtAuth(builder.Configuration, builder.Environment);
builder.Services.ConfigureRateLimiting(builder.Configuration);
builder.Services.ConfigureSecurityHeaders(builder.Configuration, builder.Environment);
builder.Services.ConfigureRequestLimits(builder.Configuration);
builder.Services.ConfigureResponseCompression(builder.Environment);
builder.Services.ConfigureResponseCaching(builder.Environment);
builder.Services.ConfigureOpenTelemetry(builder.Configuration, builder.Logging, builder.Environment);
builder.Services.ConfigureAuthServices(builder.Configuration);
builder.Services.AddScoped<TodoService>();
builder.Services.AddScoped<TagService>();

var app = builder.Build();

// Validate configuration on startup (fail fast)
ValidateConfigurationOnStartup(app.Services, app.Environment, app.Logger);

// Request/response logging (should be early in pipeline)
// This must be before GlobalExceptionHandlerMiddleware so it can capture error responses
app.UseMiddleware<RequestLoggingMiddleware>();

// Global exception handling middleware (should be early in pipeline, after logging)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Security headers (should be early, after exception handling)
app.UseMiddleware<SecurityHeadersMiddleware>();

// Rate limiting (should be early, after exception handling)
app.UseRateLimiter();

// Response compression (production only - should be early, before routing)
if (app.Environment.IsProduction())
{
    app.UseResponseCompression();
}

// no need to hide openapi docs since this is a "test" project anyways
// in a production environment, just place this in an if statement
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

// Only use HTTPS redirection in production
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Routing must come before response caching
app.UseRouting();

// Response caching (production only - must be after UseRouting but before UseAuthentication)
if (app.Environment.IsProduction())
{
    app.UseResponseCaching();
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

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
            try
            {
                db.Database.Migrate();
            }
            catch (Exception ex) when (ex.Message.Contains("pending changes") || ex.Message.Contains("PendingModelChanges"))
            {
                // Migration pending - this is expected during development when model changes haven't been migrated yet
                // In production, migrations should be applied via CI/CD or manual process
            }
        }
    }
}

app.Run();

// Make the implicit Program class public for testing
public partial class Program 
{
    /// <summary>
    /// Validates all configuration options on startup
    /// </summary>
    private static void ValidateConfigurationOnStartup(
        IServiceProvider services, 
        IHostEnvironment environment, 
        ILogger logger)
    {
        var validationErrors = new List<string>();
        var warnings = new List<string>();

        // Validate JWT settings
        ValidateOptions<JwtSettings>(services, "JWT settings", validationErrors, warnings, environment);

        // Validate Email settings
        ValidateOptions<EmailSettings>(services, "Email settings", validationErrors, warnings, environment);

        // Validate Frontend settings
        ValidateOptions<FrontendSettings>(services, "Frontend settings", validationErrors, warnings, environment);

        // Validate Rate Limiting settings
        ValidateOptions<RateLimitingSettings>(services, "Rate Limiting settings", validationErrors, warnings, environment);

        // Log warnings (non-blocking in development)
        foreach (var warning in warnings)
        {
            logger.LogWarning("Configuration warning: {Warning}", warning);
        }

        // Fail if there are validation errors
        if (validationErrors.Count > 0)
        {
            var errorMessage = "Configuration validation failed:\n" + string.Join("\n", validationErrors.Select(e => $"  - {e}"));
            logger.LogError("Configuration validation failed. Application will not start.");
            throw new InvalidOperationException(errorMessage);
        }

        if (warnings.Count > 0 && environment.IsProduction())
        {
            logger.LogWarning("Configuration warnings detected in production. Review configuration settings.");
        }
    }

    private static void ValidateOptions<T>(
        IServiceProvider services,
        string settingsName,
        List<string> errors,
        List<string> warnings,
        IHostEnvironment environment) where T : class
    {
        var options = services.GetRequiredService<IOptions<T>>();
        var validateOptions = services.GetService<IValidateOptions<T>>();

        if (validateOptions == null)
        {
            return; // No validator registered
        }

        var result = validateOptions.Validate(Options.DefaultName, options.Value);

        if (result.Failed)
        {
            foreach (var failure in result.Failures)
            {
                // In production, all validation failures are errors
                // In development, some might be warnings
                if (environment.IsProduction() || !IsOptionalInDevelopment<T>())
                {
                    errors.Add($"{settingsName}: {failure}");
                }
                else
                {
                    warnings.Add($"{settingsName}: {failure}");
                }
            }
        }
    }

    private static bool IsOptionalInDevelopment<T>() where T : class
    {
        // Email settings are optional in development
        return typeof(T) == typeof(EmailSettings);
    }
}