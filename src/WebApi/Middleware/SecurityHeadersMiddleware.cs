namespace WebApi.Middleware;

/// <summary>
/// Middleware to add security headers to all HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Legacy XSS protection (for older browsers)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions policy - restrict browser features
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

        // HSTS - only in production with HTTPS
        if (_environment.IsProduction())
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        // Content Security Policy - less critical for APIs, but still useful
        if (_environment.IsProduction())
        {
            headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
        }

        await _next(context);
    }
}
