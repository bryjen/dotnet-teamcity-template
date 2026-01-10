using System.ComponentModel.DataAnnotations;

namespace WebApi.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public required string Username { get; set; }
    
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be between 12 and 100 characters")]
    public required string Password { get; set; }
}


