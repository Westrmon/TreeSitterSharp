using TreeSitterSharp.Parser;
using TreeSitterSharp.Theme;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp;

// 这个类中是现有所有方法的统筹, 可以通过此类进行高亮, 解析返回Token, 搜索树等操作
public class TreeSitter
{
    public ParserOption Option => ParserOption.GetInstance();

    private IParser? _parser;

    public IParser Parser
    {
        get
        {
            _parser ??= ParserManager.GetInstance();
            return _parser;
        }
    }

    private ITheme? _theme;

    public ITheme Theme
    {
        get
        {
            _theme ??= SyntaxThemeManager.GetInstance();
            return _theme;
        }
    }

    private IHighlight? _highlight;

    public IHighlight Highlight
    {
        get
        {
            _highlight ??= new HighlightCode();
            return _highlight;
        }
    }

    public void LogCreate(Action<LogType, Exception?, string?>? action)
        => Log.SetLogAction(action);
}