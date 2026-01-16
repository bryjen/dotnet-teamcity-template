namespace WebApi.Configuration.Options;

/// <summary>
/// Frontend application settings
/// </summary>
public class FrontendSettings
{
    public const string SectionName = "Frontend";
    
    public string BaseUrl { get; set; } = string.Empty;
}
