namespace WebApi.Exceptions;

/// <summary>
/// Exception thrown when a resource conflict occurs (e.g., duplicate username)
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
