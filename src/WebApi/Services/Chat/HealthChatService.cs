using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Web.Common.DTOs.Health;
using WebApi.Configuration.Options;
using WebApi.Data;
using WebApi.Models;
using WebApi.Repositories;
using WebApi.Services.Chat.Plugins;
using WebApi.Services.VectorStore;

namespace WebApi.Services.Chat;

public class HealthChatService(
    Kernel kernel,
    VectorStoreService vectorStoreService,
    IServiceProvider serviceProvider,
    AppDbContext context,
    IOptions<VectorStoreSettings> vectorStoreSettings,
    ILogger<HealthChatService> logger)
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

    public async Task<HealthAssistantResponse> ProcessMessageAsync(Guid userId, string userMessage, Guid? conversationId = null)
    {
        try
        {
            // Create plugin instances per-request with userId injected
            var symptomTrackerPlugin = ActivatorUtilities.CreateInstance<SymptomTrackerPlugin>(serviceProvider, userId);
            var appointmentPlugin = ActivatorUtilities.CreateInstance<AppointmentPlugin>(serviceProvider, userId);

            // Create a new kernel for this request - reuse the base kernel's services
            // Get services from the kernel (they're registered in the kernel's service collection)
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(chatCompletionService);
            
            // Try to get embedding service if available
            try
            {
#pragma warning disable SKEXP0001
                var embeddingService = kernel.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>();
                kernelBuilder.Services.AddSingleton(embeddingService);
#pragma warning restore SKEXP0001
            }
            catch
            {
                // Embedding service might not be available, that's okay
            }
            
            var requestKernel = kernelBuilder.Build();
            
            // Add the per-request plugins with userId
            requestKernel.Plugins.AddFromObject(symptomTrackerPlugin, "SymptomTracker");
            requestKernel.Plugins.AddFromObject(appointmentPlugin, "Appointment");

            var contextMessages = await GetConversationContextAsync(conversationId);
            contextMessages = await EnrichContextWithCrossConversationMessagesAsync(userId, userMessage, conversationId, contextMessages);
            contextMessages = LimitContextMessages(contextMessages);

            var chatHistory = BuildChatHistory(contextMessages, userMessage);
            var responseText = await GetChatCompletionAsync(chatHistory, requestKernel);
            var healthResponse = ParseHealthResponse(responseText);

            return healthResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing health chat message for user {UserId}", userId);
            throw;
        }
    }

    private async Task<List<Message>> GetConversationContextAsync(Guid? conversationId)
    {
        if (conversationId.HasValue)
        {
            var messages = await vectorStoreService.GetConversationMessagesAsync(conversationId.Value);
            logger.LogDebug("Retrieved {Count} messages from conversation {ConversationId}", messages.Count, conversationId.Value);
            return messages;
        }

        logger.LogDebug("No conversation ID provided, using empty context");
        return new List<Message>();
    }

    private async Task<List<Message>> EnrichContextWithCrossConversationMessagesAsync(
        Guid userId,
        string userMessage,
        Guid? conversationId,
        List<Message> contextMessages)
    {
        var settings = vectorStoreSettings.Value;
        if (!settings.EnableCrossConversationSearch || 
            contextMessages.Count >= settings.CrossConversationSearchThreshold)
        {
            return contextMessages;
        }

        try
        {
            var relevantPastMessages = await vectorStoreService.SearchSimilarMessagesAsync(
                userId,
                userMessage,
                excludeConversationId: conversationId,
                limit: settings.MaxCrossConversationResults,
                minSimilarity: settings.MinSimilarityScore);

            if (relevantPastMessages.Any())
            {
                logger.LogDebug(
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
            logger.LogWarning(ex, "Error performing cross-conversation semantic search, using only current conversation context");
        }

        return contextMessages;
    }

    private List<Message> LimitContextMessages(List<Message> contextMessages)
    {
        var settings = vectorStoreSettings.Value;
        if (settings.MaxContextMessages > 0 && contextMessages.Count > settings.MaxContextMessages)
        {
            // Keep the most recent messages
            var limited = contextMessages
                .OrderBy(m => m.CreatedAt)
                .TakeLast(settings.MaxContextMessages)
                .ToList();
            logger.LogDebug("Limited context to {Count} most recent messages", settings.MaxContextMessages);
            return limited;
        }

        return contextMessages;
    }

    private ChatHistory BuildChatHistory(List<Message> contextMessages, string userMessage)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(SystemPrompt);

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
        chatHistory.AddUserMessage(userMessage);

        return chatHistory;
    }

    private async Task<string> GetChatCompletionAsync(ChatHistory chatHistory, Kernel requestKernel)
    {
        var chatCompletionService = requestKernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            // Enable automatic function calling - this allows the AI to invoke kernel functions
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var response = await chatCompletionService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            requestKernel,
            cancellationToken: CancellationToken.None);

        var assistantMessage = response.FirstOrDefault();
        var responseText = assistantMessage?.Content ?? "I apologize, but I couldn't generate a response.";

        logger.LogDebug("Generated AI response of length {Length} characters", responseText.Length);
        return responseText;
    }

    private HealthAssistantResponse ParseHealthResponse(string responseText)
    {
        try
        {
            // Try to extract JSON from response (might be wrapped in markdown code blocks)
            var jsonText = ExtractJsonFromResponse(responseText);
            var healthResponse = JsonSerializer.Deserialize<HealthAssistantResponse>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (healthResponse != null)
            {
                return healthResponse;
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse JSON response, using fallback");
        }

        // Fallback to plain text response
        return new HealthAssistantResponse
        {
            Message = responseText,
            Appointment = null,
            SymptomChanges = null
        };
    }

    private static string ExtractJsonFromResponse(string response)
    {
        // Remove markdown code blocks if present
        var json = response.Trim();
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            json = json.Substring(7);
        }
        if (json.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            json = json.Substring(3);
        }
        if (json.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            json = json.Substring(0, json.Length - 3);
        }
        return json.Trim();
    }
}
