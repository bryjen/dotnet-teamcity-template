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
    [Inject] private IConversationsApiClient ConversationsApiClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private DropdownService DropdownService { get; set; } = default!;

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

    // Model selector
    private const string ModelDropdownId = "model-selector";
    protected List<string> AvailableModels { get; set; } = new() { "Sonnet 4.5", "Opus 4", "Haiku 4", "Claude 3.5 Sonnet" };
    protected string SelectedModel { get; set; } = "Sonnet 4.5";
    protected bool IsModelDropdownOpen { get; set; } = false;

    protected override async Task OnInitializedAsync()
    {
        // Check authentication
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (!authState.User.Identity?.IsAuthenticated ?? true)
        {
            Navigation.NavigateTo("/auth");
            return;
        }

        // Load current user for greeting
        _ = AuthService.GetCurrentUserAsync();

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

    protected string GetGreeting()
    {
        var hour = DateTime.Now.Hour;
        var user = AuthService.CurrentUser;
        var name = user?.Email?.Split('@')[0] ?? "User";

        // Capitalize first letter
        if (!string.IsNullOrEmpty(name) && name.Length > 0)
        {
            name = char.ToUpper(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
        }

        if (hour >= 5 && hour < 12)
            return $"Good morning, {name}";
        else if (hour >= 12 && hour < 17)
            return $"Good afternoon, {name}";
        else if (hour >= 17 && hour < 21)
            return $"Evening, {name}";
        else
            return $"Good night, {name}";
    }

    protected async Task ToggleModelDropdownAsync()
    {
        if (IsModelDropdownOpen)
        {
            await CloseModelDropdownAsync();
        }
        else
        {
            await OpenModelDropdownAsync();
        }
    }

    protected async Task OpenModelDropdownAsync()
    {
        IsModelDropdownOpen = true;
        await DropdownService.OpenDropdownAsync(ModelDropdownId, async () =>
        {
            IsModelDropdownOpen = false;
            await InvokeAsync(StateHasChanged);
        });
        StateHasChanged();
    }

    protected async Task CloseModelDropdownAsync()
    {
        if (IsModelDropdownOpen)
        {
            await DropdownService.CloseDropdownAsync(ModelDropdownId);
            IsModelDropdownOpen = false;
            StateHasChanged();
        }
    }

    protected async Task SelectModel(string model)
    {
        SelectedModel = model;
        await CloseModelDropdownAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (IsModelDropdownOpen)
        {
            await DropdownService.CloseDropdownAsync(ModelDropdownId);
        }

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
