using Microsoft.AspNetCore.Components;
using Web.Common.DTOs.Auth;
using WebFrontend.Services.Auth;

namespace WebFrontend.Pages.Auth;

public partial class Register : ComponentBase
{
    [Inject] public AuthService AuthService { get; set; } = default!;
    [Inject] public AuthState AuthState { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private RegisterRequest _model = new()
    {
        Email = "",
        Password = ""
    };

    private bool _isSubmitting;
    private string? _error;

    protected override void OnInitialized()
    {
        if (AuthState.IsAuthenticated)
        {
            Nav.NavigateTo("/todos");
        }
    }

    private async Task HandleSubmit()
    {
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Email) ||
            string.IsNullOrWhiteSpace(_model.Password))
        {
            _error = "Please fill out all fields.";
            return;
        }

        _isSubmitting = true;
        try
        {
            var result = await AuthService.RegisterAsync(_model);
            if (!result.IsSuccess)
            {
                _error = result.ErrorMessage ?? "Registration failed.";
                return;
            }

            Nav.NavigateTo("/todos");
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
