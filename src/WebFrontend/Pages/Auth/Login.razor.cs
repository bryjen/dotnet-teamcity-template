using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Web.Common.DTOs.Auth;
using WebFrontend.Services.Auth;
using WebFrontend.Services.Auth.OAuth;

namespace WebFrontend.Pages.Auth;

public partial class Login : ComponentBase
{
    [Inject] public AuthService AuthService { get; set; } = null!;
    [Inject] public AuthState AuthState { get; set; } = null!;
    [Inject] public NavigationManager Nav { get; set; } = null!;
    [Inject] public IConfiguration Configuration { get; set; } = null!;
    [Inject] public IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] public OAuthService OAuthService { get; set; } = null!;
    [Inject] public OAuthProviderRegistry OAuthProviderRegistry { get; set; } = null!;

    private LoginRequest _model = new()
    {
        Email = "",
        Password = ""
    };

    private bool _isSubmitting;
    private bool _isSubmittingOAuth;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState.IsAuthenticated)
        {
            Nav.NavigateTo("/todos");
            return;
        }

        // Handle OAuth callback - check for provider in query params
        var provider = GetQueryParam("provider");
        if (!string.IsNullOrWhiteSpace(provider))
        {
            await HandleOAuthCallback(provider);
        }
    }

    private async Task HandleOAuthCallback(string providerName)
    {
        _isSubmittingOAuth = true;
        try
        {
            var callbackUri = new Uri(Nav.Uri);
            var result = await OAuthService.HandleOAuthCallback(providerName, callbackUri);

            if (!result.IsSuccess)
            {
                _error = result.ErrorMessage ?? "OAuth authentication failed.";
                _isSubmittingOAuth = false;
                return;
            }

            // Success - session already saved by AuthService.LoginWithOAuthAsync, just redirect
            var returnUrl = GetQueryParam("returnUrl");
            Nav.NavigateTo(string.IsNullOrWhiteSpace(returnUrl) ? "/todos" : returnUrl);
        }
        catch (Exception ex)
        {
            _error = $"An error occurred: {ex.Message}";
            _isSubmittingOAuth = false;
        }
    }

    private void HandleOAuthSignIn(string providerName)
    {
        var returnUrl = GetQueryParam("returnUrl");
        OAuthService.InitiateOAuthFlow(providerName, returnUrl);
    }

    private async Task HandleSubmit()
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Email) || string.IsNullOrWhiteSpace(_model.Password))
        {
            _error = "Please enter your email and password.";
            return;
        }

        _isSubmitting = true;
        try
        {
            var result = await AuthService.LoginAsync(_model);
            if (!result.IsSuccess)
            {
                _error = result.ErrorMessage ?? "Login failed.";
                return;
            }

            var returnUrl = GetQueryParam("returnUrl");
            Nav.NavigateTo(string.IsNullOrWhiteSpace(returnUrl) ? "/todos" : returnUrl);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private string? GetQueryParam(string key)
    {
        var uri = new Uri(Nav.Uri);
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
