using WebApi.DTOs.Auth;

namespace WebApi.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
}

