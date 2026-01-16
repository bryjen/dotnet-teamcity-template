using Microsoft.Extensions.Options;
using WebApi.Configuration.Options;

namespace WebApi.Configuration.Validators;

/// <summary>
/// Validates email service settings configuration
/// </summary>
public class EmailSettingsValidator : IValidateOptions<EmailSettings>
{
    private readonly IHostEnvironment _environment;

    public EmailSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, EmailSettings options)
    {
        var errors = new List<string>();

        // In production, email settings are required
        // In development, they're optional (can use mock/disabled email)
        if (_environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                errors.Add("Email ApiKey is required in production");
            }

            if (string.IsNullOrWhiteSpace(options.Domain))
            {
                errors.Add("Email Domain is required in production");
            }
        }
        // In development, just warn if missing but don't fail
        else if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.Domain))
        {
            // This will be handled as a warning, not an error
            return ValidateOptionsResult.Success;
        }

        // Validate domain format if provided
        if (!string.IsNullOrWhiteSpace(options.Domain) && 
            !options.Domain.Contains('.') && 
            options.Domain != "example.org")
        {
            errors.Add("Email Domain appears to be invalid (should be a valid domain name)");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
