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
        // Check if access token is expired or about to expire (within 1 minute)
        if (session != null && session.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
        {
            return session.AccessToken;
        }
        return null;
    }

    public async ValueTask<string?> GetRefreshTokenAsync()
    {
        var session = await GetSessionAsync();
        if (session != null && session.RefreshTokenExpiresAt > DateTime.UtcNow)
        {
            return session.RefreshToken;
        }
        return null;
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

    public async ValueTask SetSessionAsync(AuthResponse response)
    {
        var session = new AuthSession(
            response.AccessToken,
            response.RefreshToken,
            response.User,
            response.AccessTokenExpiresAt,
            response.RefreshTokenExpiresAt);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await _localStorage.SetItemAsync(StorageKey, json);
    }

    public ValueTask ClearAsync()
        => _localStorage.RemoveItemAsync(StorageKey);
}


