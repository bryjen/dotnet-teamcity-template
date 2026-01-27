using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Web.Common.DTOs.Health;
using WebApi.ApiWrapper.Services;
using WebFrontend.Services;

namespace WebFrontend.Pages;

public partial class Chat : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ITokenProvider TokenProvider { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private HubConnection? _hubConnection;
    protected List<ChatMessage> Messages { get; set; } = new();
    protected string InputText { get; set; } = string.Empty;
    protected bool IsLoading { get; set; } = false;
    protected ElementReference InputRef { get; set; }
    protected bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    private Guid? _currentConversationId;

    protected override async Task OnInitializedAsync()
    {
        // Check authentication
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (!authState.User.Identity?.IsAuthenticated ?? true)
        {
            Navigation.NavigateTo("/auth");
            return;
        }

        // Hardcode backend URL for now
        var hubUrl = "https://localhost:7265/hubs/chat";
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await TokenProvider.GetTokenAsync();
            })
            .Build();

        // Handle connection state changes
        _hubConnection.Closed += async (error) =>
        {
            await InvokeAsync(() =>
            {
                StateHasChanged();
            });
        };

        _hubConnection.Reconnecting += async (error) =>
        {
            await InvokeAsync(() =>
            {
                StateHasChanged();
            });
        };

        _hubConnection.Reconnected += async (connectionId) =>
        {
            await InvokeAsync(() =>
            {
                StateHasChanged();
            });
        };

        try
        {
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            // Handle connection error - could show error message to user
            Console.WriteLine($"SignalR connection error: {ex.Message}");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ScrollToBottom();
        }
    }

    protected async Task HandleSubmit()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsLoading || _hubConnection?.State != HubConnectionState.Connected)
            return;

        var userMessage = new ChatMessage
        {
            Content = InputText.Trim(),
            IsUser = true,
            Timestamp = DateTime.Now
        };

        Messages.Add(userMessage);
        var currentInput = InputText;
        InputText = string.Empty;
        IsLoading = true;

        StateHasChanged();
        await ScrollToBottom();

        try
        {
            // Call SignalR hub method
            var response = await _hubConnection.InvokeAsync<HealthChatResponse>("SendMessage", currentInput, _currentConversationId);

            // Update conversation ID for subsequent messages
            _currentConversationId = response.ConversationId;

            var aiMessage = new ChatMessage
            {
                Content = response.Message,
                IsUser = false,
                Timestamp = DateTime.Now
            };

            Messages.Add(aiMessage);
        }
        catch (Exception ex)
        {
            // Handle error - show error message to user
            var errorMessage = new ChatMessage
            {
                Content = $"Error: Failed to send message. {ex.Message}",
                IsUser = false,
                Timestamp = DateTime.Now
            };
            Messages.Add(errorMessage);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
            await ScrollToBottom();
        }
    }

    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await HandleSubmit();
        }
    }

    private async Task ScrollToBottom()
    {
        await Task.Delay(50);
        await JS.InvokeVoidAsync("eval", "window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });");
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

public class ChatMessage
{
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
}
