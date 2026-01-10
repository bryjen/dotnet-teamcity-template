using WebApi.DTOs.Todos;
using WebApi.Models;

namespace WebFrontend.Services.Api;

public sealed class HttpTodosApi : ITodosApi
{
    private readonly HttpApiClient _api;

    public HttpTodosApi(HttpApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<List<TodoItemDto>>> GetAllAsync(
        string? status = null,
        Priority? priority = null,
        Guid? tagId = null,
        CancellationToken ct = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(status))
        {
            parts.Add($"status={Uri.EscapeDataString(status)}");
        }
        if (priority.HasValue)
        {
            parts.Add($"priority={Uri.EscapeDataString(priority.Value.ToString())}");
        }
        if (tagId.HasValue)
        {
            parts.Add($"tagId={Uri.EscapeDataString(tagId.Value.ToString())}");
        }

        var path = parts.Count == 0 ? "/api/v1/todos" : $"/api/v1/todos?{string.Join("&", parts)}";
        return _api.GetAsync<List<TodoItemDto>>(path, ct);
    }

    public Task<ApiResult<TodoItemDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _api.GetAsync<TodoItemDto>($"/api/v1/todos/{id}", ct);

    public Task<ApiResult<TodoItemDto>> CreateAsync(CreateTodoRequest request, CancellationToken ct = default)
        => _api.PostAsync<CreateTodoRequest, TodoItemDto>("/api/v1/todos", request, ct);

    public Task<ApiResult<TodoItemDto>> UpdateAsync(Guid id, UpdateTodoRequest request, CancellationToken ct = default)
        => _api.PutAsync<UpdateTodoRequest, TodoItemDto>($"/api/v1/todos/{id}", request, ct);

    public Task<ApiResult<TodoItemDto>> ToggleCompleteAsync(Guid id, CancellationToken ct = default)
        => _api.PatchAsync<TodoItemDto>($"/api/v1/todos/{id}/complete", ct);

    public Task<ApiResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
        => _api.DeleteAsync($"/api/v1/todos/{id}", ct);
}


