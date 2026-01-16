using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Web.Common.DTOs.Auth;
using WebApi.Services;
using WebApi.Services.Auth;

namespace WebApi.Controllers;

/// <summary>
/// Handles user authentication and registration
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("auth")]
public class AuthController(
    AuthService authService, 
    PasswordResetService passwordResetService) 
    : ControllerBase
{
    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration information including username, email, and password</param>
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
    ///        "email": "john@example.com",
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
        var response = await authService.RegisterAsync(request);
        return CreatedAtAction(nameof(GetCurrentUser), response);
    }

    /// <summary>
    /// Authenticate an existing user
    /// </summary>
    /// <param name="request">Login credentials (username or email and password)</param>
    /// <returns>Authentication response with user details, access token, and refresh token</returns>
    /// <response code="200">Login successful</response>
    /// <response code="401">Invalid credentials</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/login
    ///     {
    ///        "usernameOrEmail": "johndoe",
    ///        "password": "SecurePass123!"
    ///     }
    ///
    /// You can use either username or email in the usernameOrEmail field.
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var response = await authService.LoginAsync(request);
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
        var response = await authService.RefreshTokenAsync(request.RefreshToken);
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

        var user = await authService.GetUserByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(user);
    }

    /// <summary>
    /// Request a password reset email
    /// </summary>
    /// <param name="request">Email address for password reset</param>
    /// <returns>Success response (always returns 200 for security reasons)</returns>
    /// <response code="200">Password reset email sent if email exists</response>
    /// <remarks>
    /// For security reasons, this endpoint always returns 200 OK even if the email doesn't exist.
    /// This prevents email enumeration attacks.
    ///
    /// Sample request:
    ///
    ///     POST /api/v1/auth/password-reset/request
    ///     {
    ///        "email": "user@example.com"
    ///     }
    ///
    /// </remarks>
    [HttpPost("password-reset/request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required" });
        }

        await passwordResetService.CreatePasswordResetRequest(request.Email);
        return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
    }
    
    /// <summary>
    /// Confirm password reset with token and new password
    /// </summary>
    /// <param name="request">Password reset confirmation with token and new password</param>
    /// <returns>Success response</returns>
    /// <response code="200">Password reset successful</response>
    /// <response code="400">Invalid token or password validation failed</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/password-reset/confirm
    ///     {
    ///        "token": "base64_encoded_token",
    ///        "newPassword": "NewSecurePass123!"
    ///     }
    ///
    /// Password requirements:
    /// - Minimum 12 characters
    /// - At least one uppercase letter
    /// - At least one lowercase letter
    /// - At least one number
    /// - At least one special character
    ///
    /// </remarks>
    [HttpPost("password-reset/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ConfirmPasswordReset([FromBody] ConfirmPasswordResetRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Token is required" });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { message = "New password is required" });
        }

        var result = await passwordResetService.PerformPasswordResetRequest(request.Token, request.NewPassword);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Invalid or expired token" });
        }

        return Ok(new { message = "Password has been reset successfully. You can now log in with your new password." });
    }

    public record PasswordResetRequestDto(string Email);
    public record ConfirmPasswordResetRequestDto(string Token, string NewPassword);
}
