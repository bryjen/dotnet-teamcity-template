using WebApi.DTOs.Auth;

namespace WebFrontend.Services.Auth;

public sealed class AuthState
{
    public event Action? Changed;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token) && CurrentUser != null;

    public string? Token { get; private set; }
    public UserDto? CurrentUser { get; private set; }

    public void Set(string token, UserDto user)
    {
        Token = token;
        CurrentUser = user;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        CurrentUser = null;
        Changed?.Invoke();
    }
}


