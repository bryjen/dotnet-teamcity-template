using WebApi.DTOs.Auth;
using WebFrontend.Services.Api;

namespace WebFrontend.Services.Auth;

public sealed class AuthService
{
    private readonly IAuthApi _authApi;
    private readonly TokenStore _tokenStore;
    private readonly AuthState _authState;

    public AuthService(IAuthApi authApi, TokenStore tokenStore, AuthState authState)
    {
        _authApi = authApi;
        _tokenStore = tokenStore;
        _authState = authState;
    }

    public async Task InitializeAsync()
    {
        var session = await _tokenStore.GetSessionAsync();
        if (session == null)
        {
            return;
        }

        // Optimistic restore; if backend is present later, pages can call /me to validate.
        _authState.Set(session.Token, session.User);
    }

    public async Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var result = await _authApi.RegisterAsync(request, ct);
        if (result.IsSuccess)
        {
            await SaveSessionAsync(result.Value!);
        }
        return result;
    }

    public async Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var result = await _authApi.LoginAsync(request, ct);
        if (result.IsSuccess)
        {
            await SaveSessionAsync(result.Value!);
        }
        return result;
    }

    public async Task LogoutAsync()
    {
        await _tokenStore.ClearAsync();
        _authState.Clear();
    }

    private async Task SaveSessionAsync(AuthResponse response)
    {
        await _tokenStore.SetSessionAsync(response.Token, response.User);
        _authState.Set(response.Token, response.User);
    }
}


