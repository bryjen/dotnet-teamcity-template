namespace WebApi.DTOs.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}


