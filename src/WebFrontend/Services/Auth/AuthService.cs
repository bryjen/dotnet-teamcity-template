using Web.Common.DTOs.Auth;
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

        // Check if access token is still valid
        if (session.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
        {
            // Optimistic restore; if backend is present later, pages can call /me to validate.
            _authState.Set(session.AccessToken, session.User);
        }
        else if (session.RefreshTokenExpiresAt > DateTime.UtcNow)
        {
            // Try to refresh the token
            var refreshResult = await _authApi.RefreshTokenAsync(session.RefreshToken);
            if (refreshResult.IsSuccess && refreshResult.Value != null)
            {
                await SaveSessionAsync(refreshResult.Value);
            }
            else
            {
                // Refresh failed, clear session
                await _tokenStore.ClearAsync();
                _authState.Clear();
            }
        }
        else
        {
            // Both tokens expired, clear session
            await _tokenStore.ClearAsync();
            _authState.Clear();
        }
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

    public async Task<ApiResult<AuthResponse>> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
    {
        var result = await _authApi.LoginWithGoogleAsync(idToken, ct);
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

    public async Task SaveSessionAsync(AuthResponse response)
    {
        await _tokenStore.SetSessionAsync(response);
        _authState.Set(response.AccessToken, response.User);
    }
}


