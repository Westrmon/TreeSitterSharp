using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TreeSitterSharp.Parser;

[StructLayout(LayoutKind.Auto)]
public readonly struct Token(uint startOffset, uint endOffset, string tokenKind)
{
    public uint StartOffset { get; } = startOffset;
    public uint EndOffset { get; } = endOffset;
    public string TokenKind { get; } = tokenKind;
    public uint Length => EndOffset - StartOffset;

    public bool IsEqual(uint start, uint end, string kind)
        => StartOffset == start && EndOffset == end;

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is Token other)
            return IsEqual(other.StartOffset, other.EndOffset, other.TokenKind);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(StartOffset, EndOffset);
    }

    public static bool operator ==(Token left, Token right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Token left, Token right)
    {
        return !(left == right);
    }
}