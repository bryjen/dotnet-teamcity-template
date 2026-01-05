using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebFrontend.Services.Auth;
using WebFrontend.Services.Api;
using WebFrontend.Services.Storage;
using WebFrontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Default HttpClient (same-origin) - still useful for static assets (sample-data, etc.)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Backend API HttpClient (placeholder base URL from config)
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException("Missing configuration value: Api:BaseUrl");
}

builder.Services.AddScoped<ApiHttpClient>(_ => new ApiHttpClient(new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
}));

// Auth + storage
builder.Services.AddScoped<ILocalStorage, LocalStorage>();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<ITokenProvider>(sp => sp.GetRequiredService<TokenStore>());
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<AuthService>();

// API layer
builder.Services.AddScoped<BackendStatus>();
builder.Services.AddScoped<HttpApiClient>();
builder.Services.AddScoped<IAuthApi, HttpAuthApi>();
builder.Services.AddScoped<ITodosApi, HttpTodosApi>();
builder.Services.AddScoped<ITagsApi, HttpTagsApi>();

var host = builder.Build();
await host.Services.GetRequiredService<AuthService>().InitializeAsync();
await host.RunAsync();