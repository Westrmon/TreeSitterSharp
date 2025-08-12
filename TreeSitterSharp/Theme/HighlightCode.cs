using TreeSitterSharp.Parser;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp.Theme;

internal class HighlightCode : IHighlight, IDisposable
{
    private ITheme _theme;
    private Dictionary<string, object> highlightColor;
    private IParser _parser;
    private ParserOption _option;
    private StyleTokenCache _cache;
    private readonly Log _log;

    public int MaxCacheFile
    {
        get => _cache.MaxCapacity;
        set => _cache.MaxCapacity = value;
    }

    public HighlightCode()
    {
        _theme = SyntaxThemeManager.GetInstance();
        _parser = ParserManager.GetInstance();
        _option = ParserOption.GetInstance();
        _cache = new StyleTokenCache();
        GetColorTable();
        _theme.ThemeChanged += (t, b) => GetColorTable();
    }

    public IEnumerable<StyleToken> GetCodeFileColor(string filePath)
    {
        if (_parser.GetQuery(filePath) is not IQueryTree query)
            query = _parser.ParseFile(filePath);
        return GetCodeColor(query, filePath);
    }

    public IEnumerable<StyleToken> GetCodeColor(IQueryTree query, string filePath)
    {
        var provider = _cache.GetOrAdd(
                    query,
                    key => new StyleTokenProvider(key, highlightColor, _option),
                    PathConvert.ConvertPath(filePath));
        return provider.GetColorTokens();
    }

    public void GetColor(ref StyleToken token)
        => StyleTokenProvider.ParseColorTokens(highlightColor, ref token);

    private void GetColorTable()
    {
        if (!_theme.GetConfigByMark("Highlight", out highlightColor))
            _log.LogError("Theme file is missing key: \"Highlight\"");
    }

    public void Dispose()
    {
        _cache.Dispose();
        _parser.Dispose();
    }
}