using WebApi.DTOs.Auth;

namespace WebFrontend.Services.Api;

public sealed class HttpAuthApi : IAuthApi
{
    private readonly HttpApiClient _api;

    public HttpAuthApi(HttpApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => _api.PostAsync<RegisterRequest, AuthResponse>("/api/auth/register", request, ct);

    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
        => _api.PostAsync<LoginRequest, AuthResponse>("/api/auth/login", request, ct);

    public Task<ApiResult<UserDto>> GetMeAsync(CancellationToken ct = default)
        => _api.GetAsync<UserDto>("/api/auth/me", ct);
}


