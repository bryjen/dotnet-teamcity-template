using System.Text.Json;
using WebApi.DTOs.Auth;
using WebFrontend.Services.Storage;

namespace WebFrontend.Services.Auth;

public sealed class TokenStore : ITokenProvider
{
    private const string StorageKey = "todoapp.auth.session";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILocalStorage _localStorage;

    public TokenStore(ILocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public async ValueTask<string?> GetTokenAsync()
    {
        var session = await GetSessionAsync();
        return session?.Token;
    }

    public async ValueTask<AuthSession?> GetSessionAsync()
    {
        var json = await _localStorage.GetItemAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AuthSession>(json, JsonOptions);
        }
        catch
        {
            // Corrupt/old payload - clear it.
            await _localStorage.RemoveItemAsync(StorageKey);
            return null;
        }
    }

    public async ValueTask SetSessionAsync(string token, UserDto user)
    {
        var session = new AuthSession(token, user);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await _localStorage.SetItemAsync(StorageKey, json);
    }

    public ValueTask ClearAsync()
        => _localStorage.RemoveItemAsync(StorageKey);
}


