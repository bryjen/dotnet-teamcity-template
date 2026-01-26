using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using WebApi.Configuration.Options;
using WebApi.Models;
using WebApi.Models.AI;
using WebApi.Services.VectorStore;

namespace WebApi.Services.AI.Scenarios;

/// <summary>
/// Concrete implementation of the health chat AI scenario.
/// Handles symptom tracking and appointment booking conversations.
/// </summary>
public class HealthChatScenario : AiScenarioHandler<HealthChatScenarioRequest, HealthChatScenarioResponse>
{
    private const string SystemPrompt = @"You are a helpful healthcare assistant. Your role is to:
1. Listen to users' health concerns and symptoms
2. Track symptoms using the available functions - ALWAYS call SaveSymptomAsync when a user reports a symptom
3. Assess urgency and determine if appointments are needed
4. Provide helpful health guidance

CRITICAL INSTRUCTIONS:
- When a user reports a symptom (e.g., ""I have a headache"", ""add this to my symptoms""), you MUST call the SymptomTracker_SaveSymptomAsync function immediately
- If the user asks you to add, save, or track a symptom, you MUST call the function - do not just acknowledge it
- Use SymptomTracker_GetUserSymptomsAsync to check existing symptoms when needed
- Use Appointment_BookAppointmentAsync when the user wants to book an appointment or when urgency requires it
- All functions automatically use the current user's ID - you do not need to provide a userId parameter

IMPORTANT: You must respond with valid JSON in this exact format:
{
  ""message"": ""Your response message to the user"",
  ""appointment"": {
    ""needed"": true/false,
    ""urgency"": ""Emergency"" | ""High"" | ""Medium"" | ""Low"" | ""None"",
    ""symptoms"": [""symptom1"", ""symptom2""],
    ""duration"": 30,
    ""readyToBook"": true/false,
    ""followUpNeeded"": true/false,
    ""nextQuestions"": [""question1"", ""question2""],
    ""preferredTime"": null,
    ""emergencyAction"": ""Call 911 immediately"" (only for emergencies)
  },
  ""symptomChanges"": [
    {""symptom"": ""headache"", ""action"": ""added""}
  ]
}

Always call the appropriate functions when users report symptoms or request actions. Always return valid JSON.";

    private readonly VectorStoreService _vectorStoreService;
    private readonly VectorStoreSettings _vectorStoreSettings;

    public HealthChatScenario(
        [FromKeyedServices("health")] Kernel kernel,
        VectorStoreService vectorStoreService,
        IOptions<VectorStoreSettings> vectorStoreSettings,
        ILogger<HealthChatScenario> logger)
        : base(kernel, logger)
    {
        _vectorStoreService = vectorStoreService;
        _vectorStoreSettings = vectorStoreSettings.Value;
    }

    protected override string GetSystemPrompt()
    {
        return SystemPrompt;
    }

    protected override async Task<string?> GetEmbeddingsContextAsync(
        HealthChatScenarioRequest input,
        CancellationToken cancellationToken = default)
    {
        // This method is called but we handle context enrichment in BuildChatHistory
        // since we need to work with Message objects, not just strings
        return null;
    }

    protected override ChatHistory BuildChatHistory(HealthChatScenarioRequest input, string? context)
    {
        // This method is not used since we override ExecuteAsync to handle async operations
        // But we must implement it since it's abstract
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(GetSystemPrompt());
        chatHistory.AddUserMessage(input.Message);
        return chatHistory;
    }

    public override async Task<HealthChatScenarioResponse> ExecuteAsync(
        HealthChatScenarioRequest input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get conversation context
            var contextMessages = await GetConversationContextAsync(input.ConversationId, cancellationToken);
            
            // Enrich with cross-conversation messages
            contextMessages = await EnrichContextWithCrossConversationMessagesAsync(
                input.UserId,
                input.Message,
                input.ConversationId,
                contextMessages,
                cancellationToken);
            
            // Limit context messages
            contextMessages = LimitContextMessages(contextMessages);

            // Build chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(GetSystemPrompt());

            // Add context messages in chronological order
            foreach (var msg in contextMessages.OrderBy(m => m.CreatedAt))
            {
                var role = msg.Role.ToLowerInvariant() switch
                {
                    "user" => AuthorRole.User,
                    "assistant" => AuthorRole.Assistant,
                    _ => AuthorRole.User
                };
                chatHistory.AddMessage(role, msg.Content);
            }

            // Add current user message
            chatHistory.AddUserMessage(input.Message);

            // Get AI response
            var responseText = await GetChatCompletionAsync(chatHistory, cancellationToken);

            return CreateResponse(responseText);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing health chat scenario for user {UserId}", input.UserId);
            throw;
        }
    }

    private async Task<List<Message>> GetConversationContextAsync(
        Guid? conversationId,
        CancellationToken cancellationToken)
    {
        if (conversationId.HasValue)
        {
            var messages = await _vectorStoreService.GetConversationMessagesAsync(conversationId.Value);
            Logger.LogDebug("Retrieved {Count} messages from conversation {ConversationId}", 
                messages.Count, conversationId.Value);
            return messages;
        }

        Logger.LogDebug("No conversation ID provided, using empty context");
        return new List<Message>();
    }

    private async Task<List<Message>> EnrichContextWithCrossConversationMessagesAsync(
        Guid userId,
        string userMessage,
        Guid? conversationId,
        List<Message> contextMessages,
        CancellationToken cancellationToken)
    {
        if (!_vectorStoreSettings.EnableCrossConversationSearch || 
            contextMessages.Count >= _vectorStoreSettings.CrossConversationSearchThreshold)
        {
            return contextMessages;
        }

        try
        {
            var relevantPastMessages = await _vectorStoreService.SearchSimilarMessagesAsync(
                userId,
                userMessage,
                excludeConversationId: conversationId,
                limit: _vectorStoreSettings.MaxCrossConversationResults,
                minSimilarity: _vectorStoreSettings.MinSimilarityScore);

            if (relevantPastMessages.Any())
            {
                Logger.LogDebug(
                    "Found {Count} relevant messages from other conversations (current conversation has {CurrentCount} messages)",
                    relevantPastMessages.Count,
                    contextMessages.Count);

                // Combine: past messages (semantic) + current conversation (chronological)
                // Past messages are ordered by creation date to maintain some chronological sense
                return relevantPastMessages
                    .OrderBy(m => m.CreatedAt)
                    .Concat(contextMessages)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - fall back to just current conversation context
            Logger.LogWarning(ex, 
                "Error performing cross-conversation semantic search, using only current conversation context");
        }

        return contextMessages;
    }

    private List<Message> LimitContextMessages(List<Message> contextMessages)
    {
        if (_vectorStoreSettings.MaxContextMessages > 0 && 
            contextMessages.Count > _vectorStoreSettings.MaxContextMessages)
        {
            // Keep the most recent messages
            var limited = contextMessages
                .OrderBy(m => m.CreatedAt)
                .TakeLast(_vectorStoreSettings.MaxContextMessages)
                .ToList();
            Logger.LogDebug("Limited context to {Count} most recent messages", 
                _vectorStoreSettings.MaxContextMessages);
            return limited;
        }

        return contextMessages;
    }

    protected override HealthChatScenarioResponse CreateResponse(string responseText)
    {
        return new HealthChatScenarioResponse
        {
            Message = responseText
        };
    }
}
