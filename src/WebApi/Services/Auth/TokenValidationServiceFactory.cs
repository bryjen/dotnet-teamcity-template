using WebApi.Models;

namespace WebApi.Services.Auth;

/// <summary>
/// Factory for getting the appropriate token validation service based on OAuth provider
/// </summary>
public class TokenValidationServiceFactory
{
    private readonly Dictionary<AuthProvider, ITokenValidationService> _validators;

    public TokenValidationServiceFactory(
        GoogleTokenValidationService googleValidator,
        MicrosoftTokenValidationService microsoftValidator)
    {
        _validators = new Dictionary<AuthProvider, ITokenValidationService>
        {
            { AuthProvider.Google, googleValidator },
            { AuthProvider.Microsoft, microsoftValidator }
        };
    }

    /// <summary>
    /// Gets the token validation service for the specified provider
    /// </summary>
    /// <param name="provider">The OAuth provider</param>
    /// <returns>The token validation service for the provider</returns>
    /// <exception cref="NotSupportedException">Thrown if provider is not supported</exception>
    public ITokenValidationService GetValidator(AuthProvider provider)
    {
        if (!_validators.TryGetValue(provider, out var validator))
        {
            throw new NotSupportedException($"OAuth provider '{provider}' is not supported");
        }

        return validator;
    }
}
