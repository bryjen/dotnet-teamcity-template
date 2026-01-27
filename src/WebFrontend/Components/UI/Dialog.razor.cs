using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WebFrontend.Components.UI;

public partial class Dialog : ComponentBase, IAsyncDisposable
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }
    [Parameter] public string? DialogId { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<Dialog>? _dotNetRef;
    private bool _isInitialized;

    protected override void OnInitialized()
    {
        DialogId ??= Guid.NewGuid().ToString();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/dialog.js");
        }

        if (_jsModule != null && _dotNetRef != null)
        {
            if (Open)
            {
                // Initialize dialog only once, after elements are rendered
                if (!_isInitialized)
                {
                    // Wait a bit for DOM to update
                    await Task.Delay(10);
                    await _jsModule.InvokeVoidAsync("initializeDialog", DialogId, _dotNetRef);
                    _isInitialized = true;
                }
                await _jsModule.InvokeVoidAsync("openDialog", DialogId);
            }
            else
            {
                if (_isInitialized)
                {
                    await _jsModule.InvokeVoidAsync("closeDialog", DialogId);
                }
            }
        }
    }

    public async Task OpenAsync()
    {
        Open = true;
        await OpenChanged.InvokeAsync(Open);
        StateHasChanged();
    }

    public async Task CloseAsync()
    {
        Open = false;
        await OpenChanged.InvokeAsync(Open);
        StateHasChanged();
    }

    [JSInvokable]
    public void HandleEscape()
    {
        _ = CloseAsync();
    }

    [JSInvokable]
    public void HandleOverlayClick()
    {
        _ = CloseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null && DialogId != null)
        {
            await _jsModule.InvokeVoidAsync("disposeDialog", DialogId);
            await _jsModule.DisposeAsync();
        }
        _dotNetRef?.Dispose();
    }
}
