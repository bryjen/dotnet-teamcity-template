using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApi.Controllers.Test;

/// <summary>
/// AI connection test endpoint to verify Azure OpenAI + Semantic Kernel configuration.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AiTestController(
    Kernel kernel, 
    ILogger<AiTestController> logger) : ControllerBase
{
    /// <summary>
    /// Tests the connection to Azure OpenAI by making a simple test call.
    /// </summary>
    /// <returns>Connection status and test response</returns>
    /// <response code="200">AI service is reachable and responding</response>
    /// <response code="503">AI service connection failed</response>
    /// <remarks>
    /// Verifies that the Azure OpenAI service is properly configured and reachable. Makes a simple test call to ensure connectivity.
    /// Useful for health checks and troubleshooting AI service configuration issues.
    /// 
    /// **Example Request:**
    /// ```
    /// GET /api/v1/aitest/connection
    /// ```
    /// 
    /// **Example Response (200 OK):**
    /// ```json
    /// {
    ///   "status": "connected",
    ///   "message": "Successfully reached AI service",
    ///   "response": "AI connection successful",
    ///   "timestamp": "2024-01-15T10:00:00Z"
    /// }
    /// ```
    /// 
    /// **Example Response (503 Service Unavailable):**
    /// ```json
    /// {
    ///   "status": "disconnected",
    ///   "message": "Failed to connect to AI service",
    ///   "error": "The request was aborted or the service is unreachable",
    ///   "timestamp": "2024-01-15T10:00:00Z"
    /// }
    /// ```
    /// 
    /// **Usage Notes:**
    /// - Use this endpoint to verify Azure OpenAI configuration
    /// - Check that the endpoint, deployment name, and API key are correctly configured
    /// - If this fails, check your Azure OpenAI resource settings
    /// - The test makes a minimal API call to verify connectivity
    /// </remarks>
    [HttpGet("connection")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            const string testPrompt = "Say 'AI connection successful' in exactly those words, nothing else.";
            
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(testPrompt);

            var result = await chatCompletionService.GetChatMessageContentsAsync(
                chatHistory,
                cancellationToken: HttpContext.RequestAborted);

            var responseText = result.FirstOrDefault()?.Content ?? string.Empty;
            
            logger.LogInformation("AI connection test successful. Response received: {Response}", responseText);

            return Ok(new
            {
                status = "connected",
                message = "Successfully reached AI service",
                response = responseText,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI connection test failed");
            
            return StatusCode(503, new
            {
                status = "disconnected",
                message = "Failed to connect to AI service",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Gets a programming joke from the AI model.
    /// </summary>
    /// <returns>A programming joke from the AI</returns>
    /// <response code="200">Joke retrieved successfully</response>
    /// <response code="503">AI service connection failed</response>
    /// <remarks>
    /// Retrieves a programming-related joke from the AI model. This is a fun endpoint that also serves as a more comprehensive test of the AI service functionality.
    /// 
    /// **Example Request:**
    /// ```
    /// GET /api/v1/aitest/joke
    /// ```
    /// 
    /// **Example Response (200 OK):**
    /// ```json
    /// {
    ///   "prompt": "Tell me a short programming joke",
    ///   "response": "Why do programmers prefer dark mode? Because light attracts bugs!",
    ///   "timestamp": "2024-01-15T10:00:00Z"
    /// }
    /// ```
    /// 
    /// **Example Response (503 Service Unavailable):**
    /// ```json
    /// {
    ///   "status": "error",
    ///   "message": "Failed to get joke from AI service",
    ///   "error": "The request was aborted or the service is unreachable",
    ///   "timestamp": "2024-01-15T10:00:00Z"
    /// }
    /// ```
    /// 
    /// **Usage Notes:**
    /// - This endpoint tests the full AI conversation flow
    /// - Useful for verifying that the AI model is responding correctly
    /// - The response will vary each time as it's generated by the AI
    /// - If this fails, check your Azure OpenAI deployment configuration
    /// </remarks>
    [HttpGet("joke")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetJoke()
    {
        try
        {
            const string prompt = "Tell me a short programming joke";
            
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var result = await chatCompletionService.GetChatMessageContentsAsync(
                chatHistory,
                cancellationToken: HttpContext.RequestAborted);

            var joke = result.FirstOrDefault()?.Content ?? "I couldn't think of a joke right now.";
            
            logger.LogInformation("AI joke endpoint invoked successfully. Joke length: {Length}", joke.Length);

            return Ok(new
            {
                prompt,
                response = joke,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI joke endpoint failed");
            
            return StatusCode(503, new
            {
                status = "error",
                message = "Failed to get joke from AI service",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
