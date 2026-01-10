using Microsoft.EntityFrameworkCore;
using Web.Common.DTOs.Auth;
using WebApi.Data;
using WebApi.Exceptions;
using WebApi.Models;
using WebApi.Services.Validation;

namespace WebApi.Services.Auth;

public class AuthService(
    AppDbContext context,
    JwtTokenService jwtTokenService,
    RefreshTokenService refreshTokenService,
    PasswordValidator passwordValidator,
    IConfiguration configuration)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Validate password strength
        var (isValid, errorMessage) = passwordValidator.ValidatePassword(request.Password);
        if (!isValid)
        {
            throw new ValidationException(errorMessage ?? "Invalid password");
        }

        // Check if username already exists
        if (await context.Users.AnyAsync(u => u.Username == request.Username))
        {
            throw new ConflictException("Username already exists");
        }

        // Check if email already exists
        if (await context.Users.AnyAsync(u => u.Email == request.Email))
        {
            throw new ConflictException("Email already exists");
        }

        // Hash the password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Find user by username or email
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail);

        if (user == null)
        {
            // Use same error message to prevent username enumeration
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        // Validate refresh token
        var isValid = await refreshTokenService.IsTokenValidAsync(refreshToken);
        if (!isValid)
        {
            throw new ValidationException("Invalid or expired refresh token");
        }

        var tokenEntity = await refreshTokenService.GetRefreshTokenAsync(refreshToken);
        if (tokenEntity == null || tokenEntity.User == null)
        {
            throw new ValidationException("Invalid refresh token");
        }

        // Revoke the old refresh token (token rotation)
        await refreshTokenService.RevokeRefreshTokenAsync(refreshToken, "Token rotated");

        // Generate new tokens
        return await GenerateAuthResponseAsync(tokenEntity.User);
    }
    
    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await context.Users.FindAsync(userId);
        
        if (user == null)
        {
            return null;
        }

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            CreatedAt = user.CreatedAt
        };
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var accessToken = jwtTokenService.GenerateAccessToken(user, out _);
        var refreshToken = await refreshTokenService.GenerateRefreshTokenAsync(user);

        var jwtSettings = configuration.GetSection("Jwt");
        var accessTokenExpirationMinutes = int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "15");
        var refreshTokenExpirationDays = int.Parse(jwtSettings["RefreshTokenExpirationDays"] ?? "30");

        return new AuthResponse
        {
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            },
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpirationMinutes),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays)
        };
    }
}

