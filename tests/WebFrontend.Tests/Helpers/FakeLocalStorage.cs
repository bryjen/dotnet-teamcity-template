using WebFrontend.Services.Storage;

namespace WebFrontend.Tests.Helpers;

public sealed class FakeLocalStorage : ILocalStorage
{
    private readonly Dictionary<string, string> _data = new();

    public ValueTask SetItemAsync(string key, string value)
    {
        _data[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetItemAsync(string key)
    {
        _data.TryGetValue(key, out var value);
        return ValueTask.FromResult<string?>(value);
    }

    public ValueTask RemoveItemAsync(string key)
    {
        _data.Remove(key);
        return ValueTask.CompletedTask;
    }
}





