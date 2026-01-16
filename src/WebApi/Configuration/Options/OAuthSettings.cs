namespace WebApi.Configuration.Options;

/// <summary>
/// OAuth provider settings
/// </summary>
public class OAuthSettings
{
    public const string SectionName = "OAuth";
    
    public GoogleOAuthSettings Google { get; set; } = new();
    public MicrosoftOAuthSettings Microsoft { get; set; } = new();
    public GitHubOAuthSettings GitHub { get; set; } = new();
}

public class GoogleOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
}

public class MicrosoftOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";
}

public class GitHubOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
