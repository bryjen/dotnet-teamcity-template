namespace WebApi.DTOs.Auth;

public class AuthResponse
{
    public required UserDto User { get; set; }
    public required string Token { get; set; }
}


