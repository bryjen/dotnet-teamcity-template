using Microsoft.AspNetCore.Components;
using WebFrontend.Services.Api;

namespace WebFrontend.Pages.Auth;

public partial class ForgotPassword : ComponentBase
{
    [Inject] public HttpAuthApi AuthApi { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private ForgotPasswordModel _model = new();
    private bool _isSubmitting;
    private string? _error;
    private string? _successMessage;

    private async Task HandleSubmit()
    {
        _error = null;
        _successMessage = null;

        if (string.IsNullOrWhiteSpace(_model.Email))
        {
            _error = "Please enter your email address.";
            return;
        }

        _isSubmitting = true;
        try
        {
            var result = await AuthApi.RequestPasswordResetAsync(_model.Email);
            if (!result.IsSuccess)
            {
                _error = result.ErrorMessage ?? "Failed to send reset email. Please try again.";
                return;
            }

            _successMessage = result.Value?.Message ?? "If an account with that email exists, a password reset link has been sent.";
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private class ForgotPasswordModel
    {
        public string Email { get; set; } = string.Empty;
    }
}
