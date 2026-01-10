using WebApi.DTOs.Auth;

namespace WebFrontend.Services.Auth;

public sealed record AuthSession(
    string AccessToken, 
    string RefreshToken, 
    UserDto User, 
    DateTime AccessTokenExpiresAt, 
    DateTime RefreshTokenExpiresAt);


