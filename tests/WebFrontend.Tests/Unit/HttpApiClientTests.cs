using System.Net;
using FluentAssertions;
using WebFrontend.Services.Api;
using WebFrontend.Services.Auth;

namespace WebFrontend.Tests.Unit;

[TestFixture]
public class HttpApiClientTests
{
    [Test]
    public async Task GetAsync_WhenHandlerThrowsHttpRequestException_ReturnsFriendlyFailureAndSetsBackendStatus()
    {
        var backendStatus = new BackendStatus();
        var tokenProvider = new StubTokenProvider();

        using var http = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://example/") };
        var api = new HttpApiClient(new ApiHttpClient(http), tokenProvider, backendStatus);

        var result = await api.GetAsync<object>("/api/test");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage!.Should().Contain("Backend unreachable");
        backendStatus.LastError.Should().NotBeNull();
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }

    private sealed class StubTokenProvider : ITokenProvider
    {
        public ValueTask<string?> GetTokenAsync() => ValueTask.FromResult<string?>(null);
    }
}



