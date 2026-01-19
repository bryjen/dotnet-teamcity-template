using System.Net;

namespace WebFrontend.Models;

public sealed record ApiResult<T>(
    bool IsSuccess,
    T? Value,
    HttpStatusCode? StatusCode,
    string? ErrorMessage)
{
    public static ApiResult<T> Success(T value, HttpStatusCode? statusCode = null)
        => new(true, value, statusCode, null);

    public static ApiResult<T> Failure(string message, HttpStatusCode? statusCode = null)
        => new(false, default, statusCode, message);
}


