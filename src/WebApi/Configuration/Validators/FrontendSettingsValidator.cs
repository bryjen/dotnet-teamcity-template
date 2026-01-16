using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using WebApi.Configuration.Options;

namespace WebApi.Configuration.Validators;

/// <summary>
/// Validates frontend settings configuration
/// </summary>
public class FrontendSettingsValidator : IValidateOptions<FrontendSettings>
{
    private readonly IHostEnvironment _environment;

    public FrontendSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, FrontendSettings options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            if (_environment.IsProduction())
            {
                errors.Add("Frontend BaseUrl is required in production");
            }
            // In development, allow default/example values
        }
        else
        {
            // Validate URL format if provided
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri))
            {
                errors.Add("Frontend BaseUrl must be a valid absolute URL");
            }
            else if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                     !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Frontend BaseUrl must use http or https scheme");
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
