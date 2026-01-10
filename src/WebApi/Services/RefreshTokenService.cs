using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebApi.Data;
using WebApi.Models;

namespace WebApi.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public RefreshTokenService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<string> GenerateRefreshTokenAsync(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var refreshTokenExpirationDays = int.Parse(jwtSettings["RefreshTokenExpirationDays"] ?? "30");
        
        // Revoke all existing refresh tokens for this user (token rotation)
        await RevokeAllUserTokensAsync(user.Id, "New token issued");
        
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
        
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return token;
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token);
    }

    public async Task RevokeRefreshTokenAsync(string token, string? reason = null)
    {
        var refreshToken = await GetRefreshTokenAsync(token);
        if (refreshToken != null && refreshToken.RevokedAt == null)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevocationReason = reason;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, string? reason = null)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevocationReason = reason;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsTokenValidAsync(string token)
    {
        var refreshToken = await GetRefreshTokenAsync(token);
        
        if (refreshToken == null)
        {
            return false;
        }

        if (refreshToken.RevokedAt != null)
        {
            return false;
        }

        if (refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }
}
