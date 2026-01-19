using Microsoft.AspNetCore.Components;
using Web.Common.DTOs.Auth;
using WebFrontend.Models;
using WebFrontend.Services.Api;
using WebFrontend.Services.Auth.OAuth;

namespace WebFrontend.Services.Auth;

/// <summary>
/// Service for handling OAuth authentication flows
/// </summary>
public class OAuthService(
    OAuthProviderRegistry providerRegistry,
    AuthService authService,
    NavigationManager navigationManager)
{
    /// <summary>
    /// Initiates the OAuth flow for the specified provider
    /// </summary>
    /// <param name="providerName">Name of the OAuth provider (e.g., "Google", "Microsoft")</param>
    /// <param name="returnUrl">Optional URL to redirect to after successful authentication</param>
    public void InitiateOAuthFlow(string providerName, string? returnUrl = null)
    {
        var provider = providerRegistry.GetProvider(providerName);
        if (provider == null)
        {
            throw new InvalidOperationException($"OAuth provider '{providerName}' not found or not enabled");
        }

        var redirectUri = $"{navigationManager.BaseUri}login?provider={Uri.EscapeDataString(providerName)}";
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            redirectUri += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var authUrl = provider.BuildAuthorizationUrl(redirectUri, returnUrl);
        navigationManager.NavigateTo(authUrl, forceLoad: true);
    }

    /// <summary>
    /// Handles the OAuth callback and authenticates the user
    /// </summary>
    /// <param name="providerName">Name of the OAuth provider</param>
    /// <param name="callbackUri">The callback URI from the OAuth provider</param>
    /// <returns>API result with authentication response or error</returns>
    public async Task<ApiResult<AuthResponse>> HandleOAuthCallback(string providerName, Uri callbackUri)
    {
        var provider = providerRegistry.GetProvider(providerName);
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

        // Handle authorization code flow (GitHub)
        if (!string.IsNullOrWhiteSpace(result.AuthorizationCode))
        {
            // Reconstruct redirect URI without the code/state parameters (what was sent to GitHub)
            var baseUri = new Uri(callbackUri.GetLeftPart(UriPartial.Path));
            var queryParams = new List<string> { $"provider={Uri.EscapeDataString(providerName)}" };
            var returnUrl = GetQueryParam(callbackUri, "returnUrl");
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                queryParams.Add($"returnUrl={Uri.EscapeDataString(returnUrl)}");
            }
            var redirectUriForBackend = queryParams.Count > 0 
                ? $"{baseUri}?{string.Join("&", queryParams)}"
                : baseUri.ToString();

            // Send authorization code to backend for validation and save session
            return await authService.LoginWithOAuthAsync(providerName, authorizationCode: result.AuthorizationCode, redirectUri: redirectUriForBackend);
        }
        // Handle ID token flow (Google, Microsoft)
        else if (!string.IsNullOrWhiteSpace(result.IdToken))
        {
            // Send ID token to backend for validation and save session
            return await authService.LoginWithOAuthAsync(providerName, idToken: result.IdToken);
        }
        else
        {
            return ApiResult<AuthResponse>.Failure("No authorization code or ID token received from OAuth provider");
        }
    }

    private static string? GetQueryParam(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }
        return null;
    }
}
