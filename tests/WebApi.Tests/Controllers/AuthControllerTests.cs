using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Data;
using WebApi.DTOs.Auth;
using WebApi.Models;

namespace WebApi.Tests.Controllers;

[TestFixture]
public class AuthControllerTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        // Create factory without test auth for auth tests
        _factory = new TestWebApplicationFactory(useTestAuth: false);
        _client = _factory.CreateUnauthenticatedClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Password = "NewPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be(request.Username);
    }

    [Test]
    public async Task Register_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "testuser", // Already exists in seed data
            Password = "NewPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Password = "weak" // Too short and doesn't meet complexity requirements
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Test]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "TestPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be("testuser");
    }

    [Test]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "testuser",
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "nonexistent",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetCurrentUser_WithValidToken_ReturnsUser()
    {
        // Arrange - First login to get a valid token
        var loginRequest = new LoginRequest
        {
            Username = "testuser",
            Password = "TestPassword123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {loginResult!.AccessToken}");

        // Act
        var response = await authenticatedClient.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserDto>();
        result.Should().NotBeNull();
        result!.Username.Should().Be("testuser");
    }

    [Test]
    public async Task GetCurrentUser_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetCurrentUser_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add("Authorization", "Bearer invalid-token");

        // Act
        var response = await authenticatedClient.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

