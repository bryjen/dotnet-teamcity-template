namespace WebFrontend.Services.Api;

/// <summary>
/// Dedicated HttpClient for calling the backend API (separate from the default same-origin HttpClient).
/// </summary>
public sealed class ApiHttpClient
{
    public ApiHttpClient(HttpClient client)
    {
        Client = client;
    }

    public HttpClient Client { get; }
}


