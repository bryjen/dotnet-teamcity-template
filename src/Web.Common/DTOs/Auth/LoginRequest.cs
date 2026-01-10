using System.ComponentModel.DataAnnotations;

namespace Web.Common.DTOs.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Username or email is required")]
    public required string UsernameOrEmail { get; set; }
    
    [Required(ErrorMessage = "Password is required")]
    public required string Password { get; set; }
}


