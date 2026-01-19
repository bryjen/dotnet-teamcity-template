using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using WebApi.DTOs.Tags;
using WebFrontend.Services.Api;

namespace WebFrontend.Pages.Features;

public partial class Tags : ComponentBase
{
    [Inject] public HttpTagsApi TagsApi { get; set; } = default!;

    private static readonly Regex HexColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private bool _loading = true;
    private bool _saving;
    private string? _error;

    private readonly List<TagDto> _tags = new();

    private Guid? _editingId;
    private TagEditModel _edit = new();

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
            _tags.Clear();
            var res = await TagsApi.GetAllAsync();
            if (!res.IsSuccess)
            {
                _error = res.ErrorMessage ?? "Failed to load tags.";
                return;
            }
            _tags.AddRange(res.Value!);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveTag()
    {
        _error = null;
        if (string.IsNullOrWhiteSpace(_edit.Name))
        {
            _error = "Name is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(_edit.Color) || !HexColorRegex.IsMatch(_edit.Color))
        {
            _error = "Color must be in hex format (#RRGGBB).";
            return;
        }

        _saving = true;
        try
        {
            if (_editingId == null)
            {
                var req = new CreateTagRequest { Name = _edit.Name, Color = _edit.Color };
                var res = await TagsApi.CreateAsync(req);
                if (!res.IsSuccess)
                {
                    _error = res.ErrorMessage ?? "Create failed.";
                    return;
                }
            }
            else
            {
                var req = new UpdateTagRequest { Name = _edit.Name, Color = _edit.Color };
                var res = await TagsApi.UpdateAsync(_editingId.Value, req);
                if (!res.IsSuccess)
                {
                    _error = res.ErrorMessage ?? "Update failed.";
                    return;
                }
            }

            CancelEdit();
            await Reload();
        }
        finally
        {
            _saving = false;
        }
    }

    private void StartEdit(TagDto tag)
    {
        _editingId = tag.Id;
        _edit = new TagEditModel
        {
            Name = tag.Name,
            Color = tag.Color
        };
    }

    private void CancelEdit()
    {
        _editingId = null;
        _edit = new TagEditModel();
    }

    private async Task Delete(TagDto tag)
    {
        _error = null;
        _saving = true;
        try
        {
            var res = await TagsApi.DeleteAsync(tag.Id);
            if (!res.IsSuccess)
            {
                _error = res.ErrorMessage ?? "Delete failed.";
                return;
            }
            await Reload();
        }
        finally
        {
            _saving = false;
        }
    }

    private sealed class TagEditModel
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#000000";
    }
}
