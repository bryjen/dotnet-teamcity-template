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
    TokenValidationServiceFactory tokenValidationFactory,
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

    public async Task<AuthResponse> LoginWithOAuthAsync(AuthProvider provider, string? idToken = null, string? authorizationCode = null, string? redirectUri = null)
    {
        // Get validator for the provider
        var validator = tokenValidationFactory.GetValidator(provider);

        // Get client ID and secret from configuration
        var (clientIdKey, clientSecretKey) = provider switch
        {
            AuthProvider.Google => ("OAuth:Google:ClientId", (string?)null),
            AuthProvider.Microsoft => ("OAuth:Microsoft:ClientId", (string?)null),
            AuthProvider.GitHub => ("OAuth:GitHub:ClientId", "OAuth:GitHub:ClientSecret"),
            _ => throw new NotSupportedException($"OAuth provider '{provider}' is not supported")
        };

        var clientId = configuration[clientIdKey];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException($"{provider} Client ID is not configured");
        }

        TokenValidationResult validationResult;

        // Handle authorization code flow (GitHub)
        if (!string.IsNullOrWhiteSpace(authorizationCode))
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new InvalidOperationException("Redirect URI is required for authorization code flow");
            }

            var clientSecret = !string.IsNullOrWhiteSpace(clientSecretKey) 
                ? configuration[clientSecretKey] 
                : null;

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException($"{provider} Client Secret is not configured");
            }

            validationResult = await validator.ValidateAuthorizationCodeAsync(authorizationCode, redirectUri, clientId, clientSecret);
        }
        // Handle ID token flow (Google, Microsoft)
        else if (!string.IsNullOrWhiteSpace(idToken))
        {
            validationResult = await validator.ValidateIdTokenAsync(idToken, clientId);
        }
        else
        {
            throw new InvalidOperationException("Either IdToken or AuthorizationCode must be provided");
        }

        // Use generic OAuth login method
        return await LoginWithOAuthAsync(provider, validationResult.UserId, validationResult.Email);
    }

    public async Task<AuthResponse> LoginWithOAuthAsync(AuthProvider provider, string providerUserId, string email)
    {
        // Find existing account for this provider
        var user = await context.Users
            .FirstOrDefaultAsync(u => 
                u.Provider == provider && 
                u.ProviderUserId == providerUserId);

        if (user == null)
        {
            // Check if email already exists for this provider
            if (await context.Users.AnyAsync(u => u.Provider == provider && u.Email == email))
            {
                var providerName = provider.ToString();
                throw new ConflictException($"A {providerName} account with this email already exists");
            }

            // Create new account for this provider
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = null,
                Provider = provider,
                ProviderUserId = providerUserId,
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

