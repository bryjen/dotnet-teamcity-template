using WebApi.DTOs.Todos;
using WebApi.Models;

namespace WebFrontend.Services.Api;

public interface ITodosApi
{
    Task<ApiResult<List<TodoItemDto>>> GetAllAsync(
        string? status = null,
        Priority? priority = null,
        Guid? tagId = null,
        CancellationToken ct = default);

    Task<ApiResult<TodoItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<TodoItemDto>> CreateAsync(CreateTodoRequest request, CancellationToken ct = default);
    Task<ApiResult<TodoItemDto>> UpdateAsync(Guid id, UpdateTodoRequest request, CancellationToken ct = default);
    Task<ApiResult<TodoItemDto>> ToggleCompleteAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}


