namespace TreeSitterSharp.Exceptions;

public class RepeatColorAssignment : Exception
{
    public RepeatColorAssignment(string colorName) : base($"Color {colorName} is already assigned")
    { }

    public RepeatColorAssignment() : base("Color is already assigned")
    { }
}