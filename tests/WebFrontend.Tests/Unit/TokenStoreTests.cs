using FluentAssertions;
using WebApi.DTOs.Auth;
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

        await store.SetSessionAsync("token123", user);

        var loaded = await store.GetSessionAsync();
        loaded.Should().NotBeNull();
        loaded!.Token.Should().Be("token123");
        loaded.User.Username.Should().Be("alice");

        await store.ClearAsync();

        var afterClear = await store.GetSessionAsync();
        afterClear.Should().BeNull();
    }
}





