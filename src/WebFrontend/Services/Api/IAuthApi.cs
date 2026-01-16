using Web.Common.DTOs.Auth;

namespace WebFrontend.Services.Api;

public interface IAuthApi
{
    Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ApiResult<AuthResponse>> LoginWithOAuthAsync(string provider, string? idToken = null, string? authorizationCode = null, string? redirectUri = null, CancellationToken ct = default);
    Task<ApiResult<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<ApiResult<UserDto>> GetMeAsync(CancellationToken ct = default);
    Task<ApiResult<PasswordResetResponse>> RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task<ApiResult<PasswordResetResponse>> ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken ct = default);
}

public record PasswordResetResponse(string Message);


