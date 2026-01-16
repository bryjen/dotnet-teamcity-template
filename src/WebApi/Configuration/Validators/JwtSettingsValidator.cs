using Microsoft.Extensions.Options;
using WebApi.Configuration.Options;

namespace WebApi.Configuration.Validators;

/// <summary>
/// Validates JWT settings configuration
/// </summary>
public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    private readonly IHostEnvironment _environment;

    public JwtSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        var errors = new List<string>();

        // Secret is always required
        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            errors.Add("JWT Secret is required");
        }
        else
        {
            // In production, enforce minimum secret length
            if (_environment.IsProduction() && options.Secret.Length < 32)
            {
                errors.Add("JWT Secret must be at least 32 characters for security (production requirement)");
            }
            else if (options.Secret.Length < 16)
            {
                errors.Add("JWT Secret must be at least 16 characters");
            }
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("JWT Issuer is required");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("JWT Audience is required");
        }

        if (options.AccessTokenExpirationMinutes < 1)
        {
            errors.Add("AccessTokenExpirationMinutes must be at least 1");
        }

        if (options.AccessTokenExpirationMinutes > 1440) // 24 hours
        {
            errors.Add("AccessTokenExpirationMinutes should not exceed 1440 (24 hours) for security");
        }

        if (options.RefreshTokenExpirationDays < 1)
        {
            errors.Add("RefreshTokenExpirationDays must be at least 1");
        }

        if (options.RefreshTokenExpirationDays > 365)
        {
            errors.Add("RefreshTokenExpirationDays should not exceed 365 days for security");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
