namespace WebApi.Services.Auth;

/// <summary>
/// Interface for validating OAuth ID tokens from different providers
/// </summary>
public interface ITokenValidationService
{
    /// <summary>
    /// Validates an ID token and extracts user information
    /// </summary>
    /// <param name="idToken">The ID token to validate</param>
    /// <param name="expectedClientId">The expected client ID (audience) for validation</param>
    /// <returns>Token validation result containing user ID and email</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if token validation fails</exception>
    Task<TokenValidationResult> ValidateIdTokenAsync(string idToken, string expectedClientId);
}

/// <summary>
/// Result of token validation containing extracted user information
/// </summary>
public record TokenValidationResult(string UserId, string Email);
