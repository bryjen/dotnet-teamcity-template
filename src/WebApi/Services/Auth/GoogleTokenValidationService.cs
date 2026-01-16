using Google.Apis.Auth;

namespace WebApi.Services.Auth;

/// <summary>
/// Service for validating Google ID tokens
/// </summary>
public class GoogleTokenValidationService
{
    /// <summary>
    /// Validates a Google ID token and returns the payload
    /// </summary>
    /// <param name="idToken">The Google ID token to validate</param>
    /// <param name="expectedClientId">The expected Google Client ID (audience)</param>
    /// <returns>The validated token payload containing user information</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if token validation fails</exception>
    public async Task<GoogleJsonWebSignature.Payload> ValidateIdTokenAsync(string idToken, string expectedClientId)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new UnauthorizedAccessException("ID token is required");
        }

        if (string.IsNullOrWhiteSpace(expectedClientId))
        {
            throw new InvalidOperationException("Google Client ID is not configured");
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { expectedClientId }
            };

            // This automatically fetches Google's public keys and validates the token
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return payload;
        }
        catch (InvalidJwtException ex)
        {
            throw new UnauthorizedAccessException("Invalid Google ID token", ex);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException("Failed to validate Google ID token", ex);
        }
    }
}
