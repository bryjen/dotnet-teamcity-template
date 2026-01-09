using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WebApi.Configuration;
using WebApi.Data;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.ConfigureOpenApi();
builder.Services.ConfigureDatabase(builder.Configuration, builder.Environment);
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureJwtAuth(builder.Configuration, builder.Environment);
builder.Services.ConfigureOpenTelemetry(builder.Configuration, builder.Logging, builder.Environment);

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ITagService, TagService>();

var app = builder.Build();

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