using WebApi.Models;

namespace WebApi.DTOs.Todos;

public class CreateTodoRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public Priority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public List<Guid>? TagIds { get; set; }
}


