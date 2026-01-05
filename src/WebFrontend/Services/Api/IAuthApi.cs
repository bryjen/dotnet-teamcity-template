using WebApi.DTOs.Auth;

namespace WebFrontend.Services.Api;

public interface IAuthApi
{
    Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ApiResult<UserDto>> GetMeAsync(CancellationToken ct = default);
}


