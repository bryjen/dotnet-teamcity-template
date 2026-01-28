using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebFrontend.Services;

namespace WebFrontend.Components.UI.DropdownMenu;

public partial class DropdownMenu : ComponentBase, IAsyncDisposable
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public string? MenuId { get; set; }
    [Parameter] public string Side { get; set; } = "bottom";
    [Parameter] public string Align { get; set; } = "start";
    [Parameter] public int SideOffset { get; set; } = 4;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private DropdownService DropdownService { get; set; } = default!;

    private IJSObjectReference? _jsModule;

    protected override void OnInitialized()
    {
        MenuId ??= Guid.NewGuid().ToString();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/dropdown.js");
        }

        if (_jsModule != null)
        {
            if (Open)
            {
                await _jsModule.InvokeVoidAsync("showDropdownMenu", MenuId, Side, Align, SideOffset);
            }
            else
            {
                await _jsModule.InvokeVoidAsync("hideDropdownMenu", MenuId);
            }
        }
    }

    public async Task OpenAsync()
    {
        Open = true;
        await OpenChanged.InvokeAsync(Open);
        await DropdownService.OpenDropdownAsync(MenuId ?? "", async () => await CloseAsync());
        StateHasChanged();
    }

    public async Task CloseAsync()
    {
        Open = false;
        await OpenChanged.InvokeAsync(Open);
        if (MenuId != null)
        {
            await DropdownService.CloseDropdownAsync(MenuId);
        }
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (MenuId != null)
        {
            await DropdownService.CloseDropdownAsync(MenuId);
        }
        if (_jsModule != null)
        {
            await _jsModule.DisposeAsync();
        }
    }
}
