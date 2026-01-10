namespace WebApi.Models;

public class User
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; } // Optional for future use
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<TodoItem> TodoItems { get; set; } = new List<TodoItem>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

