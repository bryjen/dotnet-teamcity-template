using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.DTOs.Auth;
using WebApi.Exceptions;
using WebApi.Models;

namespace WebApi.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IConfiguration _configuration;

    public AuthService(
        AppDbContext context, 
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IPasswordValidator passwordValidator,
        IConfiguration configuration)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _passwordValidator = passwordValidator;
        _configuration = configuration;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Validate password strength
        var (isValid, errorMessage) = _passwordValidator.ValidatePassword(request.Password);
        if (!isValid)
        {
            throw new ValidationException(errorMessage ?? "Invalid password");
        }

        // Check if username already exists
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            throw new ConflictException("Username already exists");
        }

        // Hash the password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = null, // Email optional for now
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Find user by username
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            // Use same error message to prevent username enumeration
            throw new ValidationException("Invalid username or password");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("Invalid username or password");
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        // Validate refresh token
        var isValid = await _refreshTokenService.IsTokenValidAsync(refreshToken);
        if (!isValid)
        {
            throw new ValidationException("Invalid or expired refresh token");
        }

        var tokenEntity = await _refreshTokenService.GetRefreshTokenAsync(refreshToken);
        if (tokenEntity == null || tokenEntity.User == null)
        {
            throw new ValidationException("Invalid refresh token");
        }

        // Revoke the old refresh token (token rotation)
        await _refreshTokenService.RevokeRefreshTokenAsync(refreshToken, "Token rotated");

        // Generate new tokens
        return await GenerateAuthResponseAsync(tokenEntity.User);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        
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
        var accessToken = _jwtTokenService.GenerateAccessToken(user, out _);
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user);

        var jwtSettings = _configuration.GetSection("Jwt");
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

