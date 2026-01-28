using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebApi.ApiWrapper.Extensions;
using WebApi.ApiWrapper.Services;
using WebFrontend;
using WebFrontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register HttpClient for general use
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register TokenProvider first (before API wrapper)
builder.Services.AddScoped<ITokenProvider, LocalStorageTokenProvider>();
builder.Services.AddScoped<TokenProviderHttpMessageHandler>();

// Register API wrapper with backend URL
var backendUrl = "https://localhost:7265/";
var baseUri = new Uri(backendUrl);
var jsonHeader = new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json");

// Register API clients manually to have full control
// AuthApiClient needs token provider for GetCurrentUserAsync, but token is optional for login/register
builder.Services.AddHttpClient<IAuthApiClient>((sp, client) =>
{
    client.BaseAddress = baseUri;
    client.DefaultRequestHeaders.Accept.Add(jsonHeader);
})
.AddHttpMessageHandler<TokenProviderHttpMessageHandler>()
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
.AddTypedClient<IAuthApiClient>((httpClient, sp) =>
{
    var tokenProvider = sp.GetRequiredService<ITokenProvider>();
    return new AuthApiClient(httpClient, tokenProvider);
});

// Authenticated clients need token via message handler and token provider in constructor
builder.Services.AddHttpClient<IConversationsApiClient>((sp, client) =>
{
    client.BaseAddress = baseUri;
    client.DefaultRequestHeaders.Accept.Add(jsonHeader);
})
.AddHttpMessageHandler<TokenProviderHttpMessageHandler>()
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
.AddTypedClient<IConversationsApiClient>((httpClient, sp) =>
{
    var tokenProvider = sp.GetRequiredService<ITokenProvider>();
    return new ConversationsApiClient(httpClient, tokenProvider);
});

builder.Services.AddHttpClient<IHealthChatApiClient>((sp, client) =>
{
    client.BaseAddress = baseUri;
    client.DefaultRequestHeaders.Accept.Add(jsonHeader);
})
.AddHttpMessageHandler<TokenProviderHttpMessageHandler>()
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
.AddTypedClient<IHealthChatApiClient>((httpClient, sp) =>
{
    var tokenProvider = sp.GetRequiredService<ITokenProvider>();
    return new HealthChatApiClient(httpClient, tokenProvider);
});

// Register authorization services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();
builder.Services.AddScoped<AuthService>();

// Register dropdown service
builder.Services.AddScoped<DropdownService>();

// Register dialog service as scoped
builder.Services.AddScoped<DialogService>();

// Register scroll lock service (ref-count for Dialog, Dropdown, etc.)
builder.Services.AddScoped<ScrollLockService>();

// Register toast service as singleton so it persists across components
builder.Services.AddSingleton<ToastService>();

await builder.Build().RunAsync();
