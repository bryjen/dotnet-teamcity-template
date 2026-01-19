using Microsoft.JSInterop;

namespace WebFrontend.Services.Storage;

public sealed class LocalStorage(IJSRuntime js)
{
    public ValueTask SetItemAsync(string key, string value)
        => js.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask<string?> GetItemAsync(string key)
        => js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask RemoveItemAsync(string key)
        => js.InvokeVoidAsync("localStorage.removeItem", key);
}


