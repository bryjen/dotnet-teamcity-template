using FluentAssertions;
using Web.Common.DTOs.Auth;
using WebFrontend.Services.Auth;
using WebFrontend.Tests.Helpers;

namespace WebFrontend.Tests.Unit;

[TestFixture]
public class TokenStoreTests
{
    [Test]
    public async Task RoundTrip_SaveLoadClear_Works()
    {
        var storage = new FakeLocalStorage();
        var store = new TokenStore(storage);

        var user = new UserDto
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            Email = "alice@example.com",
            CreatedAt = DateTime.UtcNow
        };

        var authResponse = new AuthResponse
        {
            AccessToken = "token123",
            RefreshToken = "refresh123",
            User = user,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await store.SetSessionAsync(authResponse);

        var loaded = await store.GetSessionAsync();
        loaded.Should().NotBeNull();
        loaded!.AccessToken.Should().Be("token123");
        loaded.RefreshToken.Should().Be("refresh123");
        loaded.User.Username.Should().Be("alice");

        await store.ClearAsync();

        var afterClear = await store.GetSessionAsync();
        afterClear.Should().BeNull();
    }
}








