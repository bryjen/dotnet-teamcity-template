using Microsoft.JSInterop;

namespace WebFrontend.Services.Storage;

public sealed class LocalStorage : ILocalStorage
{
    private readonly IJSRuntime _js;

    public LocalStorage(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask SetItemAsync(string key, string value)
        => _js.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask<string?> GetItemAsync(string key)
        => _js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask RemoveItemAsync(string key)
        => _js.InvokeVoidAsync("localStorage.removeItem", key);
}


