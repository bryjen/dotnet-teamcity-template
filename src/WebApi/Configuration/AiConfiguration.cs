using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using WebApi.Configuration.Options;
using WebApi.Services.Chat.SkPlugins;

namespace WebApi.Configuration;

public static class AiConfiguration
{
    /// <summary>
    /// Configures AI-related services, including singleton AI services and keyed kernels.
    /// Misconfiguration of Azure OpenAI will throw and fail fast.
    /// </summary>
    public static void ConfigureAi(this IServiceCollection services)
    {
        // Register singleton AI services (extracted from kernel creation)
        services.AddSingleton<IChatCompletionService>(sp =>
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

            var kernel = builder.Build();
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            logger.LogInformation("Azure OpenAI ChatCompletionService configured with deployment: {DeploymentName}", 
                settings.DeploymentName);

            return chatCompletionService;
        });

        // Register embedding service as singleton (if available)
        services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AzureOpenAiSettings>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AiConfiguration");

            var embeddingDeploymentName = settings.EmbeddingDeploymentName ?? settings.DeploymentName;
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAITextEmbeddingGeneration(
                deploymentName: embeddingDeploymentName,
                endpoint: settings.Endpoint,
                apiKey: settings.ApiKey);

            var kernel = builder.Build();
            var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

            logger.LogInformation("Azure OpenAI TextEmbeddingGenerationService configured with deployment: {EmbeddingDeploymentName}", 
                embeddingDeploymentName);

            return embeddingService;
        });

        // Register keyed scoped kernel for health chat scenario
        services.AddKeyedScoped<Kernel>("health", (sp, key) =>
        {
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var userId = ExtractUserIdFromContext(httpContext);

            var chatCompletionService = sp.GetRequiredService<IChatCompletionService>();
            var embeddingService = sp.GetService<ITextEmbeddingGenerationService>();

            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(chatCompletionService);

            if (embeddingService != null)
            {
                kernelBuilder.Services.AddSingleton(embeddingService);
            }

            var kernel = kernelBuilder.Build();

            // Create plugins with userId
            var symptomPlugin = ActivatorUtilities.CreateInstance<SymptomTrackerPlugin>(sp, userId);
            var appointmentPlugin = ActivatorUtilities.CreateInstance<AppointmentPlugin>(sp, userId);

            kernel.Plugins.AddFromObject(symptomPlugin, "SymptomTracker");
            kernel.Plugins.AddFromObject(appointmentPlugin, "Appointment");

            return kernel;
        });
    }

    /// <summary>
    /// Extracts the user ID from the HTTP context claims.
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>The user's GUID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user ID claim is missing or invalid</exception>
    private static Guid ExtractUserIdFromContext(HttpContext? context)
    {
        if (context?.User == null)
        {
            throw new UnauthorizedAccessException("User context is not available");
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid token: user ID claim is missing or invalid");
        }

        return userId;
    }
}
