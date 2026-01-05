namespace WebApi.Models;

public class Tag
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }
    
    // Foreign key
    public Guid UserId { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<TodoItem> TodoItems { get; set; } = new List<TodoItem>();
}

