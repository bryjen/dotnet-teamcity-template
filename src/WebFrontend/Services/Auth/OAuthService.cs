using Microsoft.AspNetCore.Components;
using Web.Common.DTOs.Auth;
using WebFrontend.Services.Api;
using WebFrontend.Services.Auth.OAuth;

namespace WebFrontend.Services.Auth;

/// <summary>
/// Service for handling OAuth authentication flows
/// </summary>
public class OAuthService
{
    private readonly OAuthProviderRegistry _providerRegistry;
    private readonly AuthService _authService;
    private readonly NavigationManager _navigationManager;

    public OAuthService(
        OAuthProviderRegistry providerRegistry,
        AuthService authService,
        NavigationManager navigationManager)
    {
        _providerRegistry = providerRegistry;
        _authService = authService;
        _navigationManager = navigationManager;
    }

    /// <summary>
    /// Initiates the OAuth flow for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the OAuth provider (e.g., "Google", "Microsoft")</param>
    /// <param name="returnUrl">Optional URL to redirect to after successful authentication</param>
    public void InitiateOAuthFlow(string providerName, string? returnUrl = null)
    {
        var provider = _providerRegistry.GetProvider(providerName);
        if (provider == null)
        {
            throw new InvalidOperationException($"OAuth provider '{providerName}' not found or not enabled");
        }

        var redirectUri = $"{_navigationManager.BaseUri}login?provider={Uri.EscapeDataString(providerName)}";
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            redirectUri += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var authUrl = provider.BuildAuthorizationUrl(redirectUri, returnUrl);
        _navigationManager.NavigateTo(authUrl, forceLoad: true);
    }

    /// <summary>
    /// Handles the OAuth callback and authenticates the user
    /// </summary>
    /// <param name="providerName">Name of the OAuth provider</param>
    /// <param name="callbackUri">The callback URI from the OAuth provider</param>
    /// <returns>API result with authentication response or error</returns>
    public async Task<ApiResult<AuthResponse>> HandleOAuthCallback(string providerName, Uri callbackUri)
    {
        var provider = _providerRegistry.GetProvider(providerName);
        if (provider == null)
        {
            return ApiResult<AuthResponse>.Failure($"OAuth provider '{providerName}' not found or not enabled");
        }

        var result = provider.ExtractTokenFromCallback(callbackUri);

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            var errorMessage = result.ErrorDescription ?? $"OAuth error: {result.Error}";
            return ApiResult<AuthResponse>.Failure(errorMessage);
        }

        if (string.IsNullOrWhiteSpace(result.IdToken))
        {
            return ApiResult<AuthResponse>.Failure("No ID token received from OAuth provider");
        }

        // Send ID token to backend for validation and save session
        return await _authService.LoginWithOAuthAsync(providerName, result.IdToken);
    }
}
