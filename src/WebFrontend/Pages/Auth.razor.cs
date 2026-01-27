using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using WebFrontend.Services;

namespace WebFrontend.Pages;

public partial class Auth : ComponentBase
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    protected AuthModel Model { get; set; } = new();
    protected bool IsLoading { get; set; } = false;
    protected string ErrorMessage { get; set; } = string.Empty;
    protected bool ShowPassword { get; set; } = false;

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Mode { get; set; }

    protected bool IsRegisterMode => Mode == "register" || Navigation.Uri.Contains("/register");

    protected override void OnInitialized()
    {
        // Determine mode from route
        if (Navigation.Uri.Contains("/register"))
        {
            Mode = "register";
        }
        else if (Navigation.Uri.Contains("/login"))
        {
            Mode = "login";
        }
    }

    protected void TogglePassword()
    {
        ShowPassword = !ShowPassword;
    }

    protected async Task HandleSubmit()
    {
        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            bool success;
            if (IsRegisterMode)
            {
                success = await AuthService.RegisterAsync(Model.Email, Model.Password);
            }
            else
            {
                success = await AuthService.LoginAsync(Model.Email, Model.Password);
            }

            if (success)
            {
                if (AuthStateProvider is AuthStateProvider provider)
                {
                    provider.NotifyUserChanged();
                }
                Navigation.NavigateTo("/chat");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class AuthModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(12, ErrorMessage = "Password must be at least 12 characters")]
    public string Password { get; set; } = string.Empty;
}
