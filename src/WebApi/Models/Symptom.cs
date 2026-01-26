using System.Diagnostics.CodeAnalysis;

namespace WebApi.Models;

[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class Symptom
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }                // "Cough", "Fever", "Headache"
    public string? Description { get; set; }        // "Dry cough, worse at night"
    public int Severity { get; set; }               // 1-10 scale
    public DateTime OnsetDate { get; set; }         // When it started
    public string? Frequency { get; set; }          // "Constant", "Intermittent", "Occasional"
    public List<string>? Triggers { get; set; }     // ["Exercise", "Cold weather"]
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public User? User { get; set; }
}
