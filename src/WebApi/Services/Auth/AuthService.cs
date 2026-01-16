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
    GoogleTokenValidationService googleTokenValidationService,
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

        // Check if email already exists for Local provider
        if (await context.Users.AnyAsync(u => u.Provider == AuthProvider.Local && u.Email == request.Email))
        {
            throw new ConflictException("Email already exists");
        }

        // Hash the password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            Provider = AuthProvider.Local,
            ProviderUserId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Find user by email and Local provider
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Provider == AuthProvider.Local && u.Email == request.Email);

        if (user == null)
        {
            // Use same error message to prevent email enumeration
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Verify password
        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginWithGoogleIdTokenAsync(string idToken)
    {
        // Get Google Client ID from configuration
        var googleClientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(googleClientId))
        {
            throw new InvalidOperationException("Google Client ID is not configured");
        }

        // Validate the ID token
        var payload = await googleTokenValidationService.ValidateIdTokenAsync(idToken, googleClientId);

        // Extract Google user ID (sub claim) and email
        var googleUserId = payload.Subject;
        var email = payload.Email;

        if (string.IsNullOrWhiteSpace(googleUserId))
        {
            throw new UnauthorizedAccessException("Google ID token missing user ID");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UnauthorizedAccessException("Google ID token missing email");
        }

        // Use existing LoginWithGoogleAsync method
        return await LoginWithGoogleAsync(googleUserId, email);
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(string googleUserId, string email)
    {
        // Find existing Google account
        var user = await context.Users
            .FirstOrDefaultAsync(u => 
                u.Provider == AuthProvider.Google && 
                u.ProviderUserId == googleUserId);

        if (user == null)
        {
            // Check if email already exists for Google provider
            if (await context.Users.AnyAsync(u => u.Provider == AuthProvider.Google && u.Email == email))
            {
                throw new ConflictException("A Google account with this email already exists");
            }

            // Create new Google account
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = null,
                Provider = AuthProvider.Google,
                ProviderUserId = googleUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
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

