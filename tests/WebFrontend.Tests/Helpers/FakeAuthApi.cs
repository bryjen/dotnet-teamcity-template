using WebApi.DTOs.Auth;
using WebFrontend.Services.Api;

namespace WebFrontend.Tests.Helpers;

public sealed class FakeAuthApi : IAuthApi
{
    public Func<RegisterRequest, ApiResult<AuthResponse>>? RegisterHandler { get; set; }
    public Func<LoginRequest, ApiResult<AuthResponse>>? LoginHandler { get; set; }
    public Func<ApiResult<UserDto>>? MeHandler { get; set; }

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => Task.FromResult(RegisterHandler?.Invoke(request) ?? ApiResult<AuthResponse>.Failure("Not configured"));

    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
        => Task.FromResult(LoginHandler?.Invoke(request) ?? ApiResult<AuthResponse>.Failure("Not configured"));

    public Task<ApiResult<UserDto>> GetMeAsync(CancellationToken ct = default)
        => Task.FromResult(MeHandler?.Invoke() ?? ApiResult<UserDto>.Failure("Not configured"));
}








