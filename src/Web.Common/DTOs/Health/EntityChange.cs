namespace Web.Common.DTOs.Health;

public class EntityChange
{
    public string Id { get; set; } = string.Empty; // Can be int (symptoms) or Guid (appointments) as string
    public required string Action { get; set; } // "added", "removed", "updated", "created"
}
