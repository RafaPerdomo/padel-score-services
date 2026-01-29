namespace PadelApi.Exceptions;

public class ConflictException : Exception
{
    public object? Details { get; }

    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, object details) : base(message)
    {
        Details = details;
    }
}
