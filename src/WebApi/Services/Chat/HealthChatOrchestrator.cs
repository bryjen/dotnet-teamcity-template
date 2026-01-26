using Microsoft.EntityFrameworkCore;
using Web.Common.DTOs.Health;
using WebApi.Controllers;
using WebApi.Data;
using Web.Common.DTOs.Conversations;
using WebApi.Exceptions;
using WebApi.Models;
using WebApi.Services.VectorStore;

namespace WebApi.Services.Chat;

/// <summary>
/// Orchestrates the health chat flow: conversation management, AI processing, message persistence, and embedding storage.
/// </summary>
public class HealthChatOrchestrator(
    HealthChatService healthChatService,
    ResponseRouterService responseRouter,
    VectorStoreService vectorStoreService,
    AppDbContext context,
    ILogger<HealthChatOrchestrator> logger)
{
    public async Task<(HealthChatResponse Response, bool IsNewConversation)> ProcessHealthMessageAsync(
        Guid userId,
        string message,
        Guid? conversationId = null)
    {
        var (conversation, isNewConversation) = await GetOrCreateConversationAsync(userId, message, conversationId);

        // Track state before processing to detect changes
        var symptomsBefore = await context.Symptoms
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();
        var symptomsBeforeDict = symptomsBefore.ToDictionary(s => s.Name, s => s.Id.ToString());
        var appointmentsBefore = await context.Appointments
            .Where(a => a.UserId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var healthResponse = await healthChatService.ProcessMessageAsync(
            userId,
            message,
            conversation.Id);

        var routedResponse = responseRouter.RouteResponse(healthResponse, userId);

        var (userMessage, assistantMessage) = await SaveMessagesAsync(
            conversation.Id,
            message,
            routedResponse.Message);

        await vectorStoreService.StoreMessageAsync(userId, userMessage.Id, message);
        await vectorStoreService.StoreMessageAsync(userId, assistantMessage.Id, routedResponse.Message);

        conversation.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Track changes by comparing before/after state
        var symptomChanges = await TrackSymptomChangesAsync(userId, routedResponse.SymptomChanges, symptomsBeforeDict);
        var appointmentChanges = await TrackAppointmentChangesAsync(userId, appointmentsBefore);

        var response = new HealthChatResponse
        {
            Message = routedResponse.Message,
            ConversationId = conversation.Id,
            SymptomChanges = symptomChanges,
            AppointmentChanges = appointmentChanges
        };

        return (response, isNewConversation);
    }

    private async Task<(Conversation Conversation, bool IsNewConversation)> GetOrCreateConversationAsync(
        Guid userId,
        string message,
        Guid? conversationId)
    {
        if (conversationId.HasValue)
        {
            // Continue existing conversation
            logger.LogDebug("Looking up existing conversation {ConversationId} for user {UserId}", conversationId.Value, userId);
            var conversation = await context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId.Value && c.UserId == userId);

            if (conversation == null)
            {
                logger.LogWarning("Conversation {ConversationId} not found for user {UserId}", conversationId.Value, userId);
                throw new NotFoundException("Conversation not found");
            }

            logger.LogDebug("Found existing conversation {ConversationId}, continuing conversation", conversation.Id);
            return (conversation, false);
        }
        else
        {
            // Create new conversation
            logger.LogDebug("No conversationId provided, creating new conversation for user {UserId}", userId);
            var title = message.Length > 50
                ? message.Substring(0, 50) + "..."
                : message;

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Title = title,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(); // Save to get the ID

            logger.LogDebug("Created new conversation {ConversationId} for user {UserId}", conversation.Id, userId);
            return (conversation, true);
        }
    }

    private async Task<(Message UserMessage, Message AssistantMessage)> SaveMessagesAsync(
        Guid conversationId,
        string userMessageContent,
        string assistantMessageContent)
    {
        var userMessage = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = "user",
            Content = userMessageContent,
            CreatedAt = DateTime.UtcNow
        };

        var assistantMessage = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = "assistant",
            Content = assistantMessageContent,
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(userMessage);
        context.Messages.Add(assistantMessage);
        await context.SaveChangesAsync();

        return (userMessage, assistantMessage);
    }

    private async Task<List<EntityChange>> TrackSymptomChangesAsync(
        Guid userId,
        List<SymptomChange>? symptomChangesFromAi,
        Dictionary<string, string> symptomsBeforeDict)
    {
        var changes = new List<EntityChange>();

        if (symptomChangesFromAi == null || !symptomChangesFromAi.Any())
        {
            return changes;
        }

        // Get current symptoms to find IDs
        var currentSymptoms = await context.Symptoms
            .Where(s => s.UserId == userId)
            .ToListAsync();

        foreach (var change in symptomChangesFromAi)
        {
            var symptom = currentSymptoms.FirstOrDefault(s => 
                s.Name.Equals(change.Symptom, StringComparison.OrdinalIgnoreCase));

            if (symptom != null)
            {
                var action = change.Action.ToLowerInvariant();
                
                // Determine actual action: if symptom existed before, it's "updated", otherwise "added"
                if (symptomsBeforeDict.ContainsKey(change.Symptom))
                {
                    action = action == "removed" ? "removed" : "updated";
                }
                else
                {
                    action = "added";
                }

                changes.Add(new EntityChange
                {
                    Id = symptom.Id.ToString(),
                    Action = action
                });
            }
            else if (change.Action.ToLowerInvariant() == "removed")
            {
                // Symptom was removed - find it in the before list
                if (symptomsBeforeDict.TryGetValue(change.Symptom, out var removedId))
                {
                    changes.Add(new EntityChange
                    {
                        Id = removedId,
                        Action = "removed"
                    });
                }
            }
        }

        return changes;
    }

    private async Task<List<EntityChange>> TrackAppointmentChangesAsync(
        Guid userId,
        List<Guid> appointmentsBefore)
    {
        var changes = new List<EntityChange>();

        // Find appointments created or updated in the last 30 seconds (should cover the AI processing time)
        var recentCutoff = DateTime.UtcNow.AddSeconds(-30);
        var recentAppointments = await context.Appointments
            .Where(a => a.UserId == userId && 
                       (a.CreatedAt >= recentCutoff || a.UpdatedAt >= recentCutoff))
            .ToListAsync();

        foreach (var appointment in recentAppointments)
        {
            var wasCreated = appointment.CreatedAt >= recentCutoff;
            var wasUpdated = !wasCreated && appointment.UpdatedAt >= recentCutoff;

            if (wasCreated && !appointmentsBefore.Contains(appointment.Id))
            {
                changes.Add(new EntityChange
                {
                    Id = appointment.Id.ToString(),
                    Action = "created"
                });
            }
            else if (wasUpdated && appointmentsBefore.Contains(appointment.Id))
            {
                changes.Add(new EntityChange
                {
                    Id = appointment.Id.ToString(),
                    Action = "updated"
                });
            }
        }

        return changes;
    }
}
