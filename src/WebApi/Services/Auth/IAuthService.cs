using Web.Common.DTOs.Auth;
using WebApi.Models;

namespace WebApi.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> LoginWithOAuthAsync(AuthProvider provider, string? idToken = null, string? authorizationCode = null, string? redirectUri = null);
    Task<AuthResponse> LoginWithOAuthAsync(AuthProvider provider, string providerUserId, string email);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
}
