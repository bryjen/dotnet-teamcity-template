using Web.Common.DTOs.Auth;

namespace WebFrontend.Services.Api;

public sealed class HttpAuthApi : IAuthApi
{
    private readonly HttpApiClient _api;

    public HttpAuthApi(HttpApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => _api.PostAsync<RegisterRequest, AuthResponse>("/api/v1/auth/register", request, ct);

    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
        => _api.PostAsync<LoginRequest, AuthResponse>("/api/v1/auth/login", request, ct);

    public Task<ApiResult<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var request = new RefreshTokenRequest { RefreshToken = refreshToken };
        return _api.PostAsync<RefreshTokenRequest, AuthResponse>("/api/v1/auth/refresh", request, ct);
    }

    public Task<ApiResult<UserDto>> GetMeAsync(CancellationToken ct = default)
        => _api.GetAsync<UserDto>("/api/v1/auth/me", ct);
}


