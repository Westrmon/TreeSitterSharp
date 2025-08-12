using TreeSitterSharp.Exceptions;
using TreeSitterSharp.Parser;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp.Theme;

public class StyleTokenProvider : ITokenProvider, IDisposable
{
    private Dictionary<string, object> _style;
    private IQueryTree _query;
    private ParserOption _option;

    // 现在缺少更新部分
    internal StyleTokenProvider(IQueryTree query, Dictionary<string, object> style, ParserOption option)
    {
        _style = style;
        _query = query;
        _option = option;
    }

    // 只需要重新渲染开始修改的位置
    public IEnumerable<StyleToken> GetColorTokens()
    {
        string queryFile = Path.Combine(_option.Config.QueryDir, _query.Language, "highlights.scm");
        if (!File.Exists(queryFile))
            throw new FileNotFoundException(queryFile);
        using var reader = new FileStream(queryFile, FileMode.Open, FileAccess.Read);
        foreach (var token in _query.Query(reader))
        {
            yield return ParseColorTokens(_style, token);
        }
    }

    private static StyleToken ParseColorTokens(Dictionary<string, object> style, Token token)
    {
        var styleToken = new StyleToken(token);
        ParseColorTokens(style, ref styleToken);
        return styleToken;
    }

    public static void ParseColorTokens(Dictionary<string, object> style, ref StyleToken token)
    {
        // 当前Token的颜色没有的时候, 就找他的父级
        object? value;

        string? bgColors = null, fgColors = null;
        string[]? modifiersArray = null;
        var kind = token.Token.TokenKind.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        do
        {
            style.TryGetValue(string.Join('.', kind), out value);
            kind.RemoveAt(kind.Count - 1);
        } while (value is not Dictionary<string, object> && kind.Count > 0);

        if (value == null)
            throw new Exception($"未找到颜色: {token.Token.TokenKind}");

        if (value is Dictionary<string, object> color)
        {
            if (color.TryGetValue("bg", out value))
            {
                bgColors = value switch
                {
                    string => value as string,
                    Array => throw new RepeatColorAssignment(),
                    _ => throw new Exception("未知的颜色类型")
                };
            }

            if (color.TryGetValue("fg", out value))
            {
                fgColors = color["fg"] switch
                {
                    string => color["fg"] as string,
                    Array => throw new RepeatColorAssignment(),
                    _ => throw new Exception("未知的颜色类型")
                };
            }

            if (color.TryGetValue("modifiers", out value))
            {
                modifiersArray = color["modifiers"] switch
                {
                    Array => color["modifiers"] as string[],
                    string => [color["modifiers"] as string],
                    _ => throw new Exception("未知的样式类型")
                };
            }
        }
        else if (value is string colorString)
        {
            fgColors = colorString;
        }
        token.BackgroundColorHex = bgColors;
        token.ForegroundColorHex = fgColors;
        token.FontStyle = modifiersArray;
    }

    public void Dispose()
    {
        _query.Dispose();
        _query = null;
        _option = null;
    }
}