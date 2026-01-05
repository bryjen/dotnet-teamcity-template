using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.DTOs.Tags;
using WebApi.DTOs.Todos;
using WebApi.Models;

namespace WebApi.Services;

public class TodoService : ITodoService
{
    private readonly AppDbContext _context;

    public TodoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TodoItemDto>> GetAllTodosAsync(Guid userId, string? status = null, Priority? priority = null, Guid? tagId = null)
    {
        var query = _context.TodoItems
            .Include(t => t.Tags)
            .Where(t => t.UserId == userId);

        // Filter by status
        if (!string.IsNullOrEmpty(status))
        {
            if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.IsCompleted);
            }
            else if (status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => !t.IsCompleted);
            }
        }

        // Filter by priority
        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        // Filter by tag
        if (tagId.HasValue)
        {
            query = query.Where(t => t.Tags.Any(tag => tag.Id == tagId.Value));
        }

        var todos = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return todos.Select(MapToDto).ToList();
    }

    public async Task<TodoItemDto?> GetTodoByIdAsync(Guid todoId, Guid userId)
    {
        var todo = await _context.TodoItems
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        return todo == null ? null : MapToDto(todo);
    }

    public async Task<TodoItemDto> CreateTodoAsync(CreateTodoRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Title is required");
        }

        var todo = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            DueDate = request.DueDate,
            IsCompleted = false,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add tags if specified
        if (request.TagIds != null && request.TagIds.Any())
        {
            var tags = await _context.Tags
                .Where(t => request.TagIds.Contains(t.Id) && t.UserId == userId)
                .ToListAsync();
            
            foreach (var tag in tags)
            {
                todo.Tags.Add(tag);
            }
        }

        _context.TodoItems.Add(todo);
        await _context.SaveChangesAsync();

        // Reload with tags
        await _context.Entry(todo).Collection(t => t.Tags).LoadAsync();

        return MapToDto(todo);
    }

    public async Task<TodoItemDto?> UpdateTodoAsync(Guid todoId, UpdateTodoRequest request, Guid userId)
    {
        var todo = await _context.TodoItems
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        if (todo == null)
        {
            return null;
        }

        todo.Title = request.Title;
        todo.Description = request.Description;
        todo.Priority = request.Priority;
        todo.DueDate = request.DueDate;
        todo.UpdatedAt = DateTime.UtcNow;

        // Update tags
        if (request.TagIds != null)
        {
            // Remove existing tags
            todo.Tags.Clear();

            // Add new tags
            if (request.TagIds.Any())
            {
                var tags = await _context.Tags
                    .Where(t => request.TagIds.Contains(t.Id) && t.UserId == userId)
                    .ToListAsync();
                
                foreach (var tag in tags)
                {
                    todo.Tags.Add(tag);
                }
            }
        }

        await _context.SaveChangesAsync();

        return MapToDto(todo);
    }

    public async Task<TodoItemDto?> ToggleCompleteAsync(Guid todoId, Guid userId)
    {
        var todo = await _context.TodoItems
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        if (todo == null)
        {
            return null;
        }

        todo.IsCompleted = !todo.IsCompleted;
        todo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(todo);
    }

    public async Task<bool> DeleteTodoAsync(Guid todoId, Guid userId)
    {
        var todo = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);

        if (todo == null)
        {
            return false;
        }

        _context.TodoItems.Remove(todo);
        await _context.SaveChangesAsync();

        return true;
    }

    private static TodoItemDto MapToDto(TodoItem todo)
    {
        return new TodoItemDto
        {
            Id = todo.Id,
            Title = todo.Title,
            Description = todo.Description,
            IsCompleted = todo.IsCompleted,
            Priority = todo.Priority,
            DueDate = todo.DueDate,
            Tags = todo.Tags.Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                TodoCount = 0 // Not calculated here for performance
            }).ToList(),
            CreatedAt = todo.CreatedAt,
            UpdatedAt = todo.UpdatedAt
        };
    }
}

