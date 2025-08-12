namespace TreeSitterSharp.Exceptions;

public class InvalidSuffixException : Exception
{
    public InvalidSuffixException() : base()
    {
    }

    public InvalidSuffixException(string message) : base(message)
    {
    }
}