using System.ComponentModel;
using Microsoft.SemanticKernel;
using WebApi.Models;
using WebApi.Repositories;

namespace WebApi.Services.Chat.Plugins;

public class SymptomTrackerPlugin(SymptomRepository symptomRepository, Guid userId)
{
    [KernelFunction]
    [Description("Get all symptoms for the current user. Returns a list of symptoms with their severity (1-10) and notes.")]
    public async Task<List<Symptom>> GetUserSymptomsAsync()
    {
        return await symptomRepository.GetSymptomsAsync(userId);
    }

    [KernelFunction]
    [Description("Save or update a symptom for the current user. CRITICAL: Call this function whenever a user reports a symptom, mentions a symptom, or asks you to add/track a symptom. If the symptom already exists, it will be updated. You MUST collect ALL required information before calling this function. DO NOT call this function until you have: symptom name, description, severity (1-10), onset date, frequency, and triggers (if any). Ask the user follow-up questions if any information is missing. Examples: user says 'I have a headache', 'add this to my symptoms', 'I'm experiencing nausea' - you MUST call this function, but only after gathering all required information.")]
    public async Task<Symptom> SaveSymptomAsync(
        [Description("The name of the symptom (e.g., 'headache', 'nausea', 'fever', 'cough'). REQUIRED.")] string symptomName,
        [Description("A detailed description of the symptom (e.g., 'Dry cough, worse at night', 'Throbbing pain on the right side'). REQUIRED - ask the user for details if not provided.")] string? description,
        [Description("The severity level from 1 (mild) to 10 (severe). REQUIRED. If the user doesn't specify, estimate based on their description (e.g., 'severe' = 8-10, 'moderate' = 5-7, 'mild' = 1-4).")] int severity,
        [Description("The date and time when the symptom started. REQUIRED. Format as ISO 8601 date string (YYYY-MM-DD or YYYY-MM-DDTHH:mm:ss). If the user says 'today', use today's date. If they say 'yesterday', use yesterday's date. If they say '3 days ago', calculate the date. If they don't specify, ask them when it started.")] string onsetDate,
        [Description("How often the symptom occurs. OPTIONAL. Common values: 'Constant', 'Intermittent', 'Occasional', 'Daily', 'Weekly'. Ask the user if not provided.")] string? frequency = null,
        [Description("A list of triggers that cause or worsen the symptom (e.g., ['Exercise', 'Cold weather', 'Stress']). OPTIONAL. Ask the user if they notice any triggers.")] List<string>? triggers = null)
    {
        // Parse the onset date string to DateTime
        if (!DateTime.TryParse(onsetDate, out var parsedOnsetDate))
        {
            throw new ArgumentException($"Invalid onset date format: {onsetDate}. Please use ISO 8601 format (YYYY-MM-DD or YYYY-MM-DDTHH:mm:ss).");
        }

        // Ensure onset date is in UTC
        if (parsedOnsetDate.Kind == DateTimeKind.Unspecified)
        {
            parsedOnsetDate = DateTime.SpecifyKind(parsedOnsetDate, DateTimeKind.Utc);
        }
        else if (parsedOnsetDate.Kind == DateTimeKind.Local)
        {
            parsedOnsetDate = parsedOnsetDate.ToUniversalTime();
        }

        return await symptomRepository.UpsertAsync(userId, symptomName, description, severity, parsedOnsetDate, frequency, triggers);
    }

    [KernelFunction]
    [Description("Remove a symptom for the current user by name. Returns true if the symptom was found and removed, false otherwise.")]
    public async Task<bool> RemoveSymptomAsync(
        [Description("The name of the symptom to remove")] string symptomName)
    {
        return await symptomRepository.DeleteAsync(userId, symptomName);
    }
}
