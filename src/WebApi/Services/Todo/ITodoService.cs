using WebApi.DTOs.Todos;
using WebApi.Models;

namespace WebApi.Services.Todo;

public interface ITodoService
{
    Task<List<TodoItemDto>> GetAllTodosAsync(Guid userId, string? status = null, Priority? priority = null, Guid? tagId = null);
    Task<TodoItemDto?> GetTodoByIdAsync(Guid todoId, Guid userId);
    Task<TodoItemDto> CreateTodoAsync(CreateTodoRequest request, Guid userId);
    Task<TodoItemDto?> UpdateTodoAsync(Guid todoId, UpdateTodoRequest request, Guid userId);
    Task<TodoItemDto?> ToggleCompleteAsync(Guid todoId, Guid userId);
    Task<bool> DeleteTodoAsync(Guid todoId, Guid userId);
}

