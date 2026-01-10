using System.ComponentModel.DataAnnotations;

namespace WebApi.DTOs.Auth;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Refresh token is required")]
    public required string RefreshToken { get; set; }
}
