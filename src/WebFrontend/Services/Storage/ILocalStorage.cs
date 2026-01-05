namespace WebFrontend.Services.Storage;

public interface ILocalStorage
{
    ValueTask SetItemAsync(string key, string value);
    ValueTask<string?> GetItemAsync(string key);
    ValueTask RemoveItemAsync(string key);
}


