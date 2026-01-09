using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Base controller providing common functionality for authenticated controllers
/// </summary>
[Authorize]
[ApiController]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Gets the current user's ID from the JWT token claims
    /// </summary>
    /// <returns>The user's GUID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user ID claim is missing or invalid</exception>
    protected Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid token: user ID claim is missing or invalid");
        }
        
        return userId;
    }
}
