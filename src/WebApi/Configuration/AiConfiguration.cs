using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using WebApi.Configuration.Options;
using WebApi.Services.Chat;
using WebApi.Services.Chat.Plugins;

namespace WebApi.Configuration;

public static class AiConfiguration
{
    /// <summary>
    /// Configures AI-related services, including the shared Semantic Kernel instance.
    /// Misconfiguration of Azure OpenAI will throw and fail fast.
    /// </summary>
    public static void ConfigureAi(this IServiceCollection services)
    {
        // Configure the shared Kernel instance.
        services.AddSingleton<Kernel>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AzureOpenAiSettings>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AiConfiguration");

            if (string.IsNullOrWhiteSpace(settings.Endpoint) ||
                string.IsNullOrWhiteSpace(settings.DeploymentName) ||
                string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI is not properly configured. " +
                    "Please set 'AzureOpenAI:Endpoint', 'AzureOpenAI:ApiKey', and 'AzureOpenAI:DeploymentName' in configuration.");
            }

            var builder = Kernel.CreateBuilder();

            builder.AddAzureOpenAIChatCompletion(
                deploymentName: settings.DeploymentName,
                endpoint: settings.Endpoint,
                apiKey: settings.ApiKey);

            // Add embedding service (use same deployment name if not specified)
            var embeddingDeploymentName = settings.EmbeddingDeploymentName ?? settings.DeploymentName;
#pragma warning disable SKEXP0011
            builder.AddAzureOpenAITextEmbeddingGeneration(
                deploymentName: embeddingDeploymentName,
                endpoint: settings.Endpoint,
                apiKey: settings.ApiKey);
#pragma warning restore SKEXP0011

            logger.LogInformation("Azure OpenAI Kernel configured with deployment: {DeploymentName}, embedding deployment: {EmbeddingDeploymentName}", 
                settings.DeploymentName, embeddingDeploymentName);

            var kernel = builder.Build();
            
            // Note: Plugins will be added per-request in HealthChatService since they require scoped dependencies
            // This allows plugins to access repositories and DbContext properly
            
            return kernel;
        });
    }
}
