namespace TreeSitterSharp.Exceptions;

public class InvalidToken : Exception
{
    public InvalidToken(string message) : base(message)
    { }

    public InvalidToken(string message, Exception innerException) : base(message, innerException)
    { }
}