using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Auth;

public class OAuthLoginRequest
{
    [Required(ErrorMessage = "Provider is required")]
    public required string Provider { get; set; }
    
    [Required(ErrorMessage = "ID token is required")]
    public required string IdToken { get; set; }
}
