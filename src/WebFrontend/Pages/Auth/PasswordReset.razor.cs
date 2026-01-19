using Microsoft.AspNetCore.Components;
using WebFrontend.Services.Api;

namespace WebFrontend.Pages.Auth;

public partial class PasswordReset : ComponentBase
{
    [Inject] public HttpAuthApi AuthApi { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    private PasswordResetModel _model = new();
    private bool _isSubmitting;
    private string? _error;
    private string? _successMessage;
    private string? _token;

    protected override void OnInitialized()
    {
        _token = GetQueryParam("token");
    }

    private async Task HandleSubmit()
    {
        _error = null;
        _successMessage = null;

        if (string.IsNullOrWhiteSpace(_model.NewPassword))
        {
            _error = "Please enter a new password.";
            return;
        }

        if (_model.NewPassword.Length < 12)
        {
            _error = "Password must be at least 12 characters long.";
            return;
        }

        if (_model.NewPassword != _model.ConfirmPassword)
        {
            _error = "Passwords do not match.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_token))
        {
            _error = "Invalid reset token.";
            return;
        }

        _isSubmitting = true;
        try
        {
            var result = await AuthApi.ConfirmPasswordResetAsync(_token, _model.NewPassword);
            if (!result.IsSuccess)
            {
                _error = result.ErrorMessage ?? "Failed to reset password. The token may be invalid or expired.";
                return;
            }

            _successMessage = result.Value?.Message ?? "Password has been reset successfully. You can now log in with your new password.";
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

    private class PasswordResetModel
    {
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
