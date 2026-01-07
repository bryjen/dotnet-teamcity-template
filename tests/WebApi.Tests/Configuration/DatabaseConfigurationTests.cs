using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebApi.Configuration;
using WebApi.Data;
using WebApi.DTOs.Auth;

namespace WebApi.Tests.Configuration;

[TestFixture]
public class DatabaseConfigurationTests
{
    [Test]
    public void ConfigureDatabase_WithInvalidConnectionString_FallsBackToInMemory()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=invalid-server;Database=TestDb;User Id=sa;Password=Invalid;TrustServerCertificate=true"
            })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = "Development" };

        // Act
        services.ConfigureDatabase(configuration, environment);

        // Assert - Verify in-memory database is registered
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // In-memory databases are not relational
        dbContext.Database.IsRelational().Should().BeFalse();
        dbContext.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
    }

    [Test]
    public void ConfigureDatabase_WithMissingConnectionString_FallsBackToInMemory()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // No connection string provided
            })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = "Development" };

        // Act
        services.ConfigureDatabase(configuration, environment);

        // Assert - Verify in-memory database is registered
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // In-memory databases are not relational
        dbContext.Database.IsRelational().Should().BeFalse();
        dbContext.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
    }

    [Test]
    public void ConfigureDatabase_WithValidConnectionString_ButConnectionFails_FallsBackToInMemory()
    {
        // Arrange - Use a connection string that looks valid but points to a non-existent server
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=192.0.2.1,1433;Database=NonExistentDb;User Id=sa;Password=InvalidPassword123!;TrustServerCertificate=true;Connect Timeout=5"
            })
            .Build();
        var environment = new TestHostEnvironment { EnvironmentName = "Development" };

        // Act
        services.ConfigureDatabase(configuration, environment);

        // Assert - Verify in-memory database is registered despite connection failure
        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        
        // In-memory databases are not relational
        dbContext.Database.IsRelational().Should().BeFalse();
        dbContext.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
    }

    [Test]
    public async Task Application_WithInvalidConnectionString_CanStillFunctionWithInMemoryDatabase()
    {
        // Arrange - Create a factory with invalid connection string but not in Test environment
        using var factory = new DatabaseFallbackWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act - Try to use the application (register a user)
        var registerRequest = new RegisterRequest
        {
            Username = "fallbackuser",
            Email = "fallback@example.com",
            Password = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert - Application should work with in-memory database
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.User.Username.Should().Be("fallbackuser");

        // Verify the database is in-memory by checking it's not relational
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.IsRelational().Should().BeFalse();
    }

    private class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class DatabaseFallbackWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Use Development environment (not Test) so ConfigureDatabase actually runs
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Override with an invalid connection string
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=192.0.2.1,1433;Database=NonExistentDb;User Id=sa;Password=InvalidPassword123!;TrustServerCertificate=true;Connect Timeout=5",
                    ["Jwt:Secret"] = "TestJwtSecret_ForDatabaseFallbackTests_ChangeMe_1234567890",
                    ["Jwt:Issuer"] = "TodoAppApi",
                    ["Jwt:Audience"] = "TodoAppClient",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5026"
                });
            });

            // Don't override services - let the fallback mechanism work
        }
    }
}

