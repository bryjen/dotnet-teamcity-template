using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.DTOs.Auth;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// Handles user authentication and registration
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration information including username and password</param>
    /// <returns>Authentication response with user details, access token, and refresh token</returns>
    /// <response code="201">User successfully registered</response>
    /// <response code="400">Invalid input or user already exists</response>
    /// <remarks>
    /// Password requirements:
    /// - Minimum 12 characters
    /// - At least one uppercase letter
    /// - At least one lowercase letter
    /// - At least one number
    /// - At least one special character
    ///
    /// Sample request:
    ///
    ///     POST /api/v1/auth/register
    ///     {
    ///        "username": "johndoe",
    ///        "password": "SecurePass123!"
    ///     }
    ///
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var response = await _authService.RegisterAsync(request);
        return CreatedAtAction(nameof(GetCurrentUser), response);
    }

    /// <summary>
    /// Authenticate an existing user
    /// </summary>
    /// <param name="request">Login credentials (username and password)</param>
    /// <returns>Authentication response with user details, access token, and refresh token</returns>
    /// <response code="200">Login successful</response>
    /// <response code="401">Invalid credentials</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/login
    ///     {
    ///        "username": "johndoe",
    ///        "password": "SecurePass123!"
    ///     }
    ///
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Refresh access token using a refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New authentication response with new access and refresh tokens</returns>
    /// <response code="200">Token refreshed successfully</response>
    /// <response code="400">Invalid refresh token</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/refresh
    ///     {
    ///        "refreshToken": "base64_encoded_refresh_token"
    ///     }
    ///
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var response = await _authService.RefreshTokenAsync(request.RefreshToken);
        return Ok(response);
    }

    /// <summary>
    /// Get current authenticated user's information
    /// </summary>
    /// <returns>Current user details</returns>
    /// <response code="200">User information retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">User not found</response>
    /// <remarks>
    /// Requires authentication. Include the JWT token in the Authorization header:
    /// 
    ///     Authorization: Bearer {your_token}
    /// 
    /// </remarks>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        var user = await _authService.GetUserByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(user);
    }
}

