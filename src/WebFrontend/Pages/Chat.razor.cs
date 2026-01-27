using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Web.Common.DTOs.Conversations;
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
    [Inject] private IConversationsApiClient ConversationsApiClient { get; set; } = default!;

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Conversation { get; set; }

    private HubConnection? _hubConnection;
    protected List<ChatMessage> Messages { get; set; } = new();
    protected string InputText { get; set; } = string.Empty;
    protected bool IsLoading { get; set; } = false;
    protected ElementReference InputRef { get; set; }
    protected bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    private Guid? _currentConversationId;
    private Guid? _lastLoadedConversationId;

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
            
            // Load conversation after connection is established
            await HandleConversationParameterChange();
        }
        catch (Exception ex)
        {
            // Handle connection error - could show error message to user
            Console.WriteLine($"SignalR connection error: {ex.Message}");
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        // Handle conversation parameter changes (when clicking different conversations)
        // Only process if SignalR is connected
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await HandleConversationParameterChange();
        }
    }

    private async Task HandleConversationParameterChange()
    {
        // Clear messages if no conversation parameter
        if (string.IsNullOrWhiteSpace(Conversation))
        {
            if (_currentConversationId != null)
            {
                // Starting a new chat - clear everything
                _currentConversationId = null;
                _lastLoadedConversationId = null;
                Messages.Clear();
                StateHasChanged();
            }
            return;
        }

        // Parse conversation ID
        if (!Guid.TryParse(Conversation, out var conversationId))
        {
            return;
        }

        // Only load if it's a different conversation than what's currently loaded
        if (conversationId != _lastLoadedConversationId)
        {
            await LoadConversationAsync(conversationId);
        }
    }

    private async Task LoadConversationAsync(Guid conversationId)
    {
        try
        {
            IsLoading = true;
            StateHasChanged();
            
            var conversation = await ConversationsApiClient.GetConversationByIdAsync(conversationId);
            
            if (conversation != null)
            {
                _currentConversationId = conversation.Id;
                _lastLoadedConversationId = conversation.Id;
                Messages = conversation.Messages
                    .Select(m => new ChatMessage
                    {
                        Content = m.Content,
                        IsUser = m.Role.ToLowerInvariant() == "user",
                        Timestamp = m.CreatedAt
                    })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading conversation: {ex.Message}");
            // Show error to user
            Messages.Add(new ChatMessage
            {
                Content = $"Error loading conversation: {ex.Message}",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
            await ScrollToBottom();
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
            var wasNewConversation = _currentConversationId == null;
            _currentConversationId = response.ConversationId;

            // Update URL if this is a new conversation
            if (wasNewConversation)
            {
                Navigation.NavigateTo($"/chat?conversation={_currentConversationId}", false);
            }

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
