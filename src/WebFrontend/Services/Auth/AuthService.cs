using Web.Common.DTOs.Auth;
using WebFrontend.Models;
using WebFrontend.Services.Api;

namespace WebFrontend.Services.Auth;

public sealed class AuthService(
    HttpAuthApi authApi, 
    TokenStore tokenStore, 
    AuthState authState)
{
    public async Task InitializeAsync()
    {
        var session = await tokenStore.GetSessionAsync();
        if (session == null)
        {
            return;
        }

        // Check if access token is still valid
        if (session.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
        {
            // Optimistic restore; if backend is present later, pages can call /me to validate.
            authState.Set(session.AccessToken, session.User);
        }
        else if (session.RefreshTokenExpiresAt > DateTime.UtcNow)
        {
            // Try to refresh the token
            var refreshResult = await authApi.RefreshTokenAsync(session.RefreshToken);
            if (refreshResult.IsSuccess && refreshResult.Value != null)
            {
                await SaveSessionAsync(refreshResult.Value);
            }
            else
            {
                // Refresh failed, clear session
                await tokenStore.ClearAsync();
                authState.Clear();
            }
        }
        else
        {
            // Both tokens expired, clear session
            await tokenStore.ClearAsync();
            authState.Clear();
        }
    }

    public async Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var result = await authApi.RegisterAsync(request, ct);
        if (result.IsSuccess)
        {
            await SaveSessionAsync(result.Value!);
        }
        return result;
    }

    public async Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var result = await authApi.LoginAsync(request, ct);
        if (result.IsSuccess)
        {
            await SaveSessionAsync(result.Value!);
        }
        return result;
    }

    public async Task<ApiResult<AuthResponse>> LoginWithOAuthAsync(string provider, string? idToken = null, string? authorizationCode = null, string? redirectUri = null, CancellationToken ct = default)
    {
        var result = await authApi.LoginWithOAuthAsync(provider, idToken, authorizationCode, redirectUri, ct);
        if (result.IsSuccess)
        {
            await SaveSessionAsync(result.Value!);
        }
        return result;
    }

    public async Task LogoutAsync()
    {
        await tokenStore.ClearAsync();
        authState.Clear();
    }

    public async Task SaveSessionAsync(AuthResponse response)
    {
        await tokenStore.SetSessionAsync(response);
        authState.Set(response.AccessToken, response.User);
    }
}


