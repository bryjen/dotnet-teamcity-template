using Microsoft.JSInterop;

namespace WebFrontend.Services;

/// <summary>
/// Service for managing dropdown menus with click-outside detection.
/// </summary>
public class DropdownService : IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, Action> _openDropdowns = new();
    private IJSObjectReference? _jsModule;
    private bool _isInitialized;

    public DropdownService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Opens a dropdown with the given ID and registers click-outside handler.
    /// </summary>
    public async Task OpenDropdownAsync(string dropdownId, Action onClose)
    {
        // Close any other open dropdowns
        await CloseAllAsync();

        _openDropdowns[dropdownId] = onClose;

        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        // Wait a bit for DOM to update, then register
        await Task.Delay(10);
        
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("registerClickOutside", dropdownId, DotNetObjectReference.Create(this));
            }
            catch (JSException)
            {
                // JavaScript not ready yet, will retry on next interaction
            }
        }
    }

    /// <summary>
    /// Closes a specific dropdown.
    /// </summary>
    public async Task CloseDropdownAsync(string dropdownId)
    {
        if (_openDropdowns.Remove(dropdownId) && _jsModule != null)
        {
            await _jsModule.InvokeVoidAsync("unregisterClickOutside", dropdownId);
        }
    }

    /// <summary>
    /// Closes all open dropdowns.
    /// </summary>
    public async Task CloseAllAsync()
    {
        var dropdownIds = _openDropdowns.Keys.ToList();
        foreach (var id in dropdownIds)
        {
            await CloseDropdownAsync(id);
        }
    }

    /// <summary>
    /// Called from JavaScript when a click outside is detected.
    /// </summary>
    [JSInvokable]
    public void HandleClickOutside(string dropdownId)
    {
        if (_openDropdowns.TryGetValue(dropdownId, out var onClose))
        {
            onClose.Invoke();
            _openDropdowns.Remove(dropdownId);
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/dropdown.js");
            _isInitialized = true;
        }
        catch (JSException)
        {
            // JavaScript module not loaded yet, will retry
        }
    }

    public void Dispose()
    {
        _openDropdowns.Clear();
        _jsModule?.DisposeAsync();
    }
}
