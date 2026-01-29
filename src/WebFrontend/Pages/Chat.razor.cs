using System;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Web.Common.DTOs.Health;
using WebApi.ApiWrapper.Services;
using WebFrontend.Components.UI.Select;
using WebFrontend.Services;

namespace WebFrontend.Pages;

public partial class Chat : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IConversationsApiClient ConversationsApiClient { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private ChatHubClient ChatHubClient { get; set; } = default!;

    [Parameter]
    [SupplyParameterFromQuery]
    public string? Conversation { get; set; }

    protected List<ChatMessage> Messages { get; set; } = new();
    protected string InputText { get; set; } = string.Empty;
    protected bool IsLoading { get; set; } = false;
    protected bool IsConnected => ChatHubClient.IsConnected;
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

        // Load current user for greeting
        _ = AuthService.GetCurrentUserAsync();

        try
        {
            await ChatHubClient.ConnectAsync();

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
        if (ChatHubClient.IsConnected)
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
                    .OrderBy(m => m.CreatedAt)
                    .ThenBy(m => GetRoleSortOrder(m.Role))
                    .ThenBy(m => m.Id)
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
        if (string.IsNullOrWhiteSpace(InputText) || IsLoading || !ChatHubClient.IsConnected)
        {
            return;
        }

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
            var response = await ChatHubClient.SendMessageAsync(currentInput, _currentConversationId);

            var wasNewConversation = _currentConversationId == null;
            _currentConversationId = response.ConversationId;

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

    protected void OnInputTextChanged(string value)
    {
        InputText = value;
    }

    private async Task ScrollToBottom()
    {
        await Task.Delay(50);
        await JS.InvokeVoidAsync("eval", "window.scrollTo({ top: document.body.ScrollHeight, behavior: 'smooth' });");
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

        // TEMP, TODO REMOVE WHEN WE HAVE USERS AND STUFF LIKE THAT
        name = "John";

        return hour switch
        {
            >= 5 and < 12 => $"Good morning, {name}",
            >= 12 and < 17 => $"Good afternoon, {name}",
            _ => $"Evening, {name}"
        };
    }

    private static int GetRoleSortOrder(string role)
    {
        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    public async ValueTask DisposeAsync()
    {
        await ChatHubClient.DisposeAsync();
    }
}
