using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using WebFrontend.Attributes;
using WebFrontend.Components;
using WebFrontend.Layout;
using WebFrontend.Services.Auth;

namespace WebFrontend.Tests.Component;

[TestFixture]
public class AuthRouteViewTests
{
    [Test]
    public void RequireAuthPage_WhenUnauthenticated_RedirectsToLoginWithReturnUrl()
    {
        using var ctx = new Bunit.TestContext();
        var authState = new AuthState(); // unauthenticated by default
        ctx.Services.AddSingleton(authState);

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/todos");

        var routeData = new RouteData(typeof(ProtectedPage), new Dictionary<string, object?>());

        ctx.RenderComponent<AuthRouteView>(ps => ps
            .Add(p => p.RouteData, routeData)
            .Add(p => p.DefaultLayout, typeof(MainLayout)));

        nav.Uri.Should().Contain("/login?returnUrl=");
    }

    [RequireAuth]
    private sealed class ProtectedPage : ComponentBase
    {
    }
}


