using WebApi.Models;

namespace WebApi.Services;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshTokenAsync(User user);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token, string? reason = null);
    Task RevokeAllUserTokensAsync(Guid userId, string? reason = null);
    Task<bool> IsTokenValidAsync(string token);
}
