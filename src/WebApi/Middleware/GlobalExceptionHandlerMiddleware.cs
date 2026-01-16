using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Cors.Infrastructure;
using WebApi.DTOs;
using WebApi.Exceptions;

namespace WebApi.Middleware;

/// <summary>
/// Global exception handling middleware for truly unexpected exceptions only.
/// Domain exceptions (ValidationException, NotFoundException, ConflictException, UnauthorizedAccessException)
/// should be handled by controllers and returned as standardized ErrorResponse.
/// </summary>
public class GlobalExceptionHandlerMiddleware(
    RequestDelegate next, 
    ILogger<GlobalExceptionHandlerMiddleware> logger,
    ICorsService corsService,
    ICorsPolicyProvider corsPolicyProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Check if this is a domain exception that should have been handled by controllers
            if (ex is ValidationException or NotFoundException or ConflictException or UnauthorizedAccessException)
            {
                // Domain exceptions should be caught by controllers, but handle them here as a safety net
                // Log a warning to indicate controllers should handle these
                logger.LogWarning(
                    "Domain exception {ExceptionType} reached middleware. Controllers should handle this: {Message}",
                    ex.GetType().Name,
                    ex.Message);
                await HandleDomainExceptionAsync(context, ex);
                return;
            }

            // Handle truly unexpected exceptions (NullReferenceException, database failures, etc.)
            logger.LogError(ex, "An unhandled exception occurred: {ExceptionType}", ex.GetType().Name);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleDomainExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        
        // Check if response has already started - if so, we can't modify it
        if (response.HasStarted)
        {
            logger.LogWarning("Cannot write error response - response has already started");
            return;
        }

        // Clear any existing response content
        response.Clear();

        // Apply CORS headers before writing the response
        var policy = await corsPolicyProvider.GetPolicyAsync(context, null);
        if (policy != null)
        {
            var corsResult = corsService.EvaluatePolicy(context, policy);
            corsService.ApplyResult(corsResult, response);
        }

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

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        try
        {
            await response.WriteAsync(jsonResponse);
            await response.Body.FlushAsync();
        }
        catch (Exception writeEx)
        {
            logger.LogError(writeEx, "Failed to write error response to client");
            if (!response.HasStarted)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        
        // Check if response has already started - if so, we can't modify it
        if (response.HasStarted)
        {
            logger.LogWarning("Cannot write error response - response has already started");
            return;
        }

        // Clear any existing response content
        response.Clear();

        // Apply CORS headers before writing the response
        // This ensures error responses include CORS headers so the browser doesn't block them
        var policy = await corsPolicyProvider.GetPolicyAsync(context, null);
        if (policy != null)
        {
            var corsResult = corsService.EvaluatePolicy(context, policy);
            corsService.ApplyResult(corsResult, response);
        }

        response.ContentType = "application/json";
        response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // Always mask internal error details in production
        var errorResponse = new ErrorResponse
        {
            Message = "An error occurred while processing your request."
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Ensure we can write to the response
        try
        {
            await response.WriteAsync(jsonResponse);
            await response.Body.FlushAsync();
        }
        catch (Exception writeEx)
        {
            logger.LogError(writeEx, "Failed to write error response to client");
            // If we can't write, at least try to set the status code if it hasn't been set
            if (!response.HasStarted)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }
    }
}
