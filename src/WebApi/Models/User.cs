using System.Diagnostics.CodeAnalysis;

namespace WebApi.Models;

[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }
    public required AuthProvider Provider { get; set; }
    public string? ProviderUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetRequest> PasswordResetRequests { get; set; } = new List<PasswordResetRequest>();
}