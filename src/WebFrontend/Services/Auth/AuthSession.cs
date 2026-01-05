using WebApi.DTOs.Auth;

namespace WebFrontend.Services.Auth;

public sealed record AuthSession(string Token, UserDto User);


