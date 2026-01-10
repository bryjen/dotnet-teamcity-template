using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Models;
using WebApi.Services.Email;

namespace WebApi.Services.Auth;

public class PasswordResetService(
    AppDbContext context,
    IEmailService emailService,
    string frontendBaseUrl)
{
    public async Task CreatePasswordResetRequest(string email)
    {
        var emailProcessed = email.Trim();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == emailProcessed);
        if (user is null)
            return;
        
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var created = DateTime.UtcNow;
        var willExpireOn = created + TimeSpan.FromHours(1);

        var passwordResetRequest = new PasswordResetRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            ExpiresAt = willExpireOn,
            CreatedAt = created,
            User = user
        };

        await context.PasswordResetRequests.AddAsync(passwordResetRequest);
        var _ = await context.SaveChangesAsync();
        
        var baseUri = new Uri(frontendBaseUrl);
        var resetUrl = new Uri(baseUri, "/auth/password-reset?token=" + token).ToString();
        await emailService.SendPasswordResetEmailAsync(email, resetUrl, CancellationToken.None);
    }
    
    public Task PerformPasswordResetRequest()
    {
        throw new NotImplementedException();
    }
}