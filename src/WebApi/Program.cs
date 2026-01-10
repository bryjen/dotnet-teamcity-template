using Microsoft.EntityFrameworkCore;
using WebApi.Configuration;
using WebApi.Data;
using WebApi.Middleware;
using WebApi.Services.Auth;
using WebApi.Services.Email;
using WebApi.Services.Tag;
using WebApi.Services.Todo;
using WebApi.Services.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(ServiceConfiguration.ConfigureJsonCallback);

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

builder.Services.ConfigureEmail(builder.Configuration);

builder.Services.ConfigureOpenApi();
builder.Services.ConfigureDatabase(builder.Configuration, builder.Environment);
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureJwtAuth(builder.Configuration, builder.Environment);
builder.Services.ConfigureOpenTelemetry(builder.Configuration, builder.Logging, builder.Environment);

builder.Services.AddScoped<PasswordResetService>(sp =>
{
    var frontendUrl = builder.Configuration["Frontend:BaseUrl"] ?? throw new InvalidOperationException("Frontend URL not configured");
    return new PasswordResetService(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IEmailService>(), frontendUrl);
});

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<PasswordValidator>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ITagService, TagService>();

var app = builder.Build();

// Global exception handling middleware (should be early in pipeline)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

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

app.UseHttpsRedirection();

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
public partial class Program { }