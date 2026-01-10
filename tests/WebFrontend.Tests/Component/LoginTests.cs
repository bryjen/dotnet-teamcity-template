using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WebApi.DTOs.Auth;
using WebFrontend.Pages;
using WebFrontend.Services.Api;
using WebFrontend.Services.Auth;
using WebFrontend.Services.Storage;
using WebFrontend.Tests.Helpers;

namespace WebFrontend.Tests.Component;

[TestFixture]
public class LoginTests
{
    [Test]
    public void SubmittingEmptyForm_ShowsError()
    {
        using var ctx = new Bunit.TestContext();
        RegisterCommon(ctx);

        var cut = ctx.RenderComponent<Login>();
        cut.Find("button[type='submit']").Click();

        cut.Markup.Should().Contain("Please enter your username and password.");
    }

    [Test]
    public async Task SuccessfulLogin_NavigatesToReturnUrl()
    {
        using var ctx = new Bunit.TestContext();
        var authApi = new FakeAuthApi
        {
            LoginHandler = _ => ApiResult<AuthResponse>.Success(new AuthResponse
            {
                AccessToken = "token",
                RefreshToken = "refresh",
                User = new UserDto
                {
                    Id = Guid.NewGuid(),
                    Username = "alice",
                    Email = "alice@example.com",
                    CreatedAt = DateTime.UtcNow
                },
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(15),
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30)
            })
        };

        RegisterCommon(ctx, authApi);

        // Navigate to login with returnUrl
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?returnUrl=%2Ftodos");

        var cut = ctx.RenderComponent<Login>();

        var inputs = cut.FindAll("input");
        inputs[0].Change("alice");
        inputs = cut.FindAll("input"); // re-query after re-render
        inputs[1].Change("password");

        await cut.InvokeAsync(() => cut.Find("button[type='submit']").Click());

        nav.Uri.Should().EndWith("/todos");
    }

    private static void RegisterCommon(Bunit.TestContext ctx, FakeAuthApi? authApi = null)
    {
        ctx.Services.AddSingleton<ILocalStorage>(new FakeLocalStorage());
        ctx.Services.AddSingleton<AuthState>();
        ctx.Services.AddSingleton(authApi ?? new FakeAuthApi());
        ctx.Services.AddSingleton<WebFrontend.Services.Api.IAuthApi>(sp => sp.GetRequiredService<FakeAuthApi>());
        ctx.Services.AddSingleton<TokenStore>();
        ctx.Services.AddSingleton<AuthService>();
    }
}


