namespace TreeSitterSharp.Theme;

public class SyntaxThemeManager : ITheme
{
    private IThemeProvider _themeProvider;

    private static SyntaxThemeManager? _instance;
    private static Lock _lock = new();

    private SyntaxThemeManager()
    { }

    public static ITheme GetInstance()
    {
        using (_lock.EnterScope())
            _instance ??= new SyntaxThemeManager();
        return _instance;
    }

    public event Action<ITheme, bool>? ThemeChanged;

    public void RegisterThemeProvider(IThemeProvider provider)
    {
        _themeProvider = provider;
        provider.OnThemePropertyChanged += ()
            => ThemeChanged?.Invoke(this, false);
    }

    public void AddExternalConfig(string mark)
        => _themeProvider.AddExternConfigKey(mark);

    public bool GetConfigByMark(string mark, out Dictionary<string, object>? style)
        => _themeProvider.GetConfigByMark(mark, out style);
}