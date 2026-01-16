namespace WebApi.Configuration.Options;

/// <summary>
/// Email service settings (Resend)
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email:Resend";
    
    public string ApiKey { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
}
