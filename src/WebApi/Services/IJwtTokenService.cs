using WebApi.Models;

namespace WebApi.Services;

/// <summary>
/// Service for generating JWT tokens
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(User user);
}
