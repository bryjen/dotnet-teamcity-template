namespace WebFrontend.Services.Auth;

public interface ITokenProvider
{
    ValueTask<string?> GetTokenAsync();
}


