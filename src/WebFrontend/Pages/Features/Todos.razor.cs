using Microsoft.AspNetCore.Components;
using WebApi.DTOs.Tags;
using WebApi.DTOs.Todos;
using WebApi.Models;
using WebFrontend.Services.Api;

namespace WebFrontend.Pages.Features;

public partial class Todos : ComponentBase
{
    [Inject] public HttpTodosApi TodosApi { get; set; } = default!;
    [Inject] public HttpTagsApi TagsApi { get; set; } = default!;

    private bool _loading = true;
    private bool _saving;
    private string? _error;

    private readonly List<TagDto> _tags = new();
    private readonly List<TodoItemDto> _todos = new();

    private string _filterStatus = "";
    private Priority? _filterPriority = null;
    private string _filterTagId = "";

    private Guid? _editingId;
    private TodoEditModel _edit = new();

    protected override async Task OnInitializedAsync()
    {
        await Reload();
    }

    private async Task Reload()
    {
        _error = null;
        _loading = true;
        try
        {
            await LoadTags();
            await LoadTodos();
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadTags()
    {
        _tags.Clear();
        var res = await TagsApi.GetAllAsync();
        if (!res.IsSuccess)
        {
            _error = res.ErrorMessage ?? "Failed to load tags.";
            return;
        }
        _tags.AddRange(res.Value!);
    }

    private async Task LoadTodos()
    {
        _todos.Clear();

        Guid? tagId = null;
        if (!string.IsNullOrWhiteSpace(_filterTagId) && Guid.TryParse(_filterTagId, out var parsed))
        {
            tagId = parsed;
        }

        var res = await TodosApi.GetAllAsync(
            status: string.IsNullOrWhiteSpace(_filterStatus) ? null : _filterStatus,
            priority: _filterPriority,
            tagId: tagId);

        if (!res.IsSuccess)
        {
            _error = res.ErrorMessage ?? "Failed to load todos.";
            return;
        }

        _todos.AddRange(res.Value!);
    }

    private async Task SaveTodo()
    {
        _error = null;
        if (string.IsNullOrWhiteSpace(_edit.Title))
        {
            _error = "Title is required.";
            return;
        }

        _saving = true;
        try
        {
            if (_editingId == null)
            {
                var req = new CreateTodoRequest
                {
                    Title = _edit.Title,
                    Description = _edit.Description,
                    Priority = _edit.Priority,
                    DueDate = _edit.DueDate,
                    TagIds = _edit.TagIds.Count == 0 ? null : _edit.TagIds.ToList()
                };

                var res = await TodosApi.CreateAsync(req);
                if (!res.IsSuccess)
                {
                    _error = res.ErrorMessage ?? "Create failed.";
                    return;
                }
            }
            else
            {
                var req = new UpdateTodoRequest
                {
                    Title = _edit.Title,
                    Description = _edit.Description,
                    Priority = _edit.Priority,
                    DueDate = _edit.DueDate,
                    TagIds = _edit.TagIds.Count == 0 ? new List<Guid>() : _edit.TagIds.ToList()
                };

                var res = await TodosApi.UpdateAsync(_editingId.Value, req);
                if (!res.IsSuccess)
                {
                    _error = res.ErrorMessage ?? "Update failed.";
                    return;
                }
            }

            CancelEdit();
            await LoadTodos();
        }
        finally
        {
            _saving = false;
        }
    }

    private void StartEdit(TodoItemDto todo)
    {
        _editingId = todo.Id;
        _edit = new TodoEditModel
        {
            Title = todo.Title,
            Description = todo.Description,
            Priority = todo.Priority,
            DueDate = todo.DueDate,
            TagIds = todo.Tags.Select(t => t.Id).ToHashSet()
        };
    }

    private void CancelEdit()
    {
        _editingId = null;
        _edit = new TodoEditModel();
    }

    private async Task Toggle(TodoItemDto todo)
    {
        _error = null;
        _saving = true;
        try
        {
            var res = await TodosApi.ToggleCompleteAsync(todo.Id);
            if (!res.IsSuccess)
            {
                _error = res.ErrorMessage ?? "Toggle failed.";
                return;
            }
            await LoadTodos();
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task Delete(TodoItemDto todo)
    {
        _error = null;
        _saving = true;
        try
        {
            var res = await TodosApi.DeleteAsync(todo.Id);
            if (!res.IsSuccess)
            {
                _error = res.ErrorMessage ?? "Delete failed.";
                return;
            }
            await LoadTodos();
        }
        finally
        {
            _saving = false;
        }
    }

    private void OnTagsChanged(ChangeEventArgs e)
    {
        // For <select multiple>, Blazor provides a string[] in most browsers.
        if (e.Value is string[] values)
        {
            _edit.TagIds = values
                .Select(v => Guid.TryParse(v, out var id) ? (Guid?)id : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            return;
        }

        // Fallback: sometimes we get a single string.
        if (e.Value is string value && Guid.TryParse(value, out var single))
        {
            _edit.TagIds = new HashSet<Guid> { single };
        }
    }

    private sealed class TodoEditModel
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public Priority Priority { get; set; } = Priority.Medium;
        public DateTime? DueDate { get; set; }
        public HashSet<Guid> TagIds { get; set; } = new();
    }
}
