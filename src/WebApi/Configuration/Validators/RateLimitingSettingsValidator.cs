using Microsoft.Extensions.Options;
using WebApi.Configuration.Options;

namespace WebApi.Configuration.Validators;

/// <summary>
/// Validates rate limiting settings configuration
/// </summary>
public class RateLimitingSettingsValidator : IValidateOptions<RateLimitingSettings>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingSettings options)
    {
        var errors = new List<string>();

        // Validate Global policy
        ValidatePolicy(options.Global, "Global", errors);
        
        // Validate Auth policy
        ValidatePolicy(options.Auth, "Auth", errors);
        
        // Validate Authenticated policy
        ValidatePolicy(options.Authenticated, "Authenticated", errors);

        // Ensure Auth policy is stricter than Global
        if (options.Auth.PermitLimit >= options.Global.PermitLimit)
        {
            errors.Add("Auth rate limit (PermitLimit) should be stricter than Global limit");
        }

        // Ensure Authenticated policy allows more than Global
        if (options.Authenticated.PermitLimit <= options.Global.PermitLimit)
        {
            errors.Add("Authenticated rate limit (PermitLimit) should be higher than Global limit");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidatePolicy(RateLimitPolicy policy, string policyName, List<string> errors)
    {
        if (policy.PermitLimit < 1)
        {
            errors.Add($"{policyName} rate limit PermitLimit must be at least 1");
        }

        if (policy.PermitLimit > 10000)
        {
            errors.Add($"{policyName} rate limit PermitLimit should not exceed 10000");
        }

        if (policy.WindowMinutes < 1)
        {
            errors.Add($"{policyName} rate limit WindowMinutes must be at least 1");
        }

        if (policy.WindowMinutes > 60)
        {
            errors.Add($"{policyName} rate limit WindowMinutes should not exceed 60");
        }

        if (policy.QueueLimit < 0)
        {
            errors.Add($"{policyName} rate limit QueueLimit must be 0 or greater");
        }
    }
}
