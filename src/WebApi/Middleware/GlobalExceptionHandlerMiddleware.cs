using System.Net;
using System.Text.Json;
using WebApi.DTOs;
using WebApi.Exceptions;

namespace WebApi.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses
/// </summary>
public class GlobalExceptionHandlerMiddleware(
    RequestDelegate next, 
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred: {ExceptionType}", ex.GetType().Name);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Message = exception.Message
        };

        response.StatusCode = exception switch
        {
            ValidationException => (int)HttpStatusCode.BadRequest,
            NotFoundException => (int)HttpStatusCode.NotFound,
            ConflictException => (int)HttpStatusCode.Conflict,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            _ => (int)HttpStatusCode.InternalServerError
        };

        // Don't expose internal error details in production
        if (response.StatusCode == (int)HttpStatusCode.InternalServerError)
        {
            errorResponse.Message = "An error occurred while processing your request.";
        }

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return response.WriteAsync(jsonResponse);
    }
}
