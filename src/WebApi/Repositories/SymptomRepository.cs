using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;

namespace WebApi.Repositories;

public class SymptomRepository(AppDbContext context)
{
    public async Task<List<Symptom>> GetSymptomsAsync(Guid userId)
    {
        return await context.Symptoms
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Symptom> UpsertAsync(
        Guid userId, 
        string symptomName, 
        string? description,
        int severity, 
        DateTime onsetDate,
        string? frequency = null,
        List<string>? triggers = null)
    {
        var existing = await context.Symptoms
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Name == symptomName);

        if (existing != null)
        {
            existing.Description = description;
            existing.Severity = severity;
            existing.OnsetDate = onsetDate;
            existing.Frequency = frequency;
            existing.Triggers = triggers;
            existing.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return existing;
        }

        var symptom = new Symptom
        {
            UserId = userId,
            Name = symptomName,
            Description = description,
            Severity = severity,
            OnsetDate = onsetDate,
            Frequency = frequency,
            Triggers = triggers,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Symptoms.Add(symptom);
        await context.SaveChangesAsync();
        return symptom;
    }

    public async Task<bool> DeleteAsync(Guid userId, string symptomName)
    {
        var symptom = await context.Symptoms
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Name == symptomName);

        if (symptom == null)
        {
            return false;
        }

        context.Symptoms.Remove(symptom);
        await context.SaveChangesAsync();
        return true;
    }
}
