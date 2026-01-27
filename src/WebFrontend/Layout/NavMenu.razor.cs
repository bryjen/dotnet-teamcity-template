using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WebFrontend.Services;

namespace WebFrontend.Layout;

public partial class NavMenu : ComponentBase
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    protected bool CollapseNavMenu { get; set; } = true;

    protected void ToggleNavMenu()
    {
        CollapseNavMenu = !CollapseNavMenu;
    }

    protected async Task HandleLogout()
    {
        await AuthService.LogoutAsync();
        if (AuthStateProvider is AuthStateProvider provider)
        {
            provider.NotifyUserChanged();
        }
        Navigation.NavigateTo("/auth");
    }

    protected string GetUsername(AuthenticationState authState)
    {
        // Try to get from AuthService first (more reliable - has email)
        var user = AuthService.CurrentUser;
        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            return user.Email;
        }

        // Fallback to claims (email claim)
        var emailClaim = authState.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(emailClaim))
        {
            return emailClaim;
        }

        // Last fallback to name claim
        return authState.User?.Identity?.Name ?? "User";
    }
}
