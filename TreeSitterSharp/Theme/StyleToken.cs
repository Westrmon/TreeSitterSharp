using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using TreeSitterSharp.Parser;

namespace TreeSitterSharp.Theme;

[StructLayout(LayoutKind.Auto)]
public struct StyleToken
{
    private string? _bgColor;
    private string? _fgColor;
    public Token Token { get; }

    public string? BackgroundColorHex
    {
        readonly get => _bgColor;
        set
        {
            _bgColor = value;
        }
    }

    public string? ForegroundColorHex
    {
        readonly get => _fgColor;
        set
        {
            _fgColor = value;
        }
    }

    public char? BackgroundColor { get; set; }

    public char? ForegroundColor { get; set; }

    public string[]? FontStyle { get; set; }

    public readonly uint Length => Token.Length;
    public readonly bool HaveFontStyle => FontStyle != null;

    public StyleToken(Token token, string? bgColor = null, string? fgColor = null, string[]? fontStyle = null)
    {
        Token = token;
        _bgColor = bgColor;
        _fgColor = fgColor;
        FontStyle = fontStyle;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is StyleToken other)
            return Token.StartOffset == other.Token.StartOffset && Token.EndOffset == other.Token.EndOffset;
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Token.StartOffset, Token.EndOffset);
    }

    public override string ToString()
    {
        return @$"[{Token.StartOffset}:{Token.EndOffset}]
bg:{(_bgColor != null ? string.Join(',', _bgColor) : "null")}
fg:{(_fgColor != null ? string.Join(',', _fgColor) : "null")}
style:{(HaveFontStyle ? string.Join(',', FontStyle) : "null")}";
    }

    public static bool operator ==(StyleToken left, StyleToken right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(StyleToken left, StyleToken right)
    {
        return !(left == right);
    }
}