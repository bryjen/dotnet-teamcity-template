using System.Security.Claims;
using WebApi.Models;

namespace WebApi.Services;

/// <summary>
/// Service for generating JWT tokens
/// </summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(User user, out string jti);
    ClaimsPrincipal? ValidateToken(string token);
}
