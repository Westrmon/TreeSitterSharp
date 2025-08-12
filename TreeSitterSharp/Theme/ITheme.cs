namespace TreeSitterSharp.Theme;

public interface ITheme
{
    bool GetConfigByMark(string mark, out Dictionary<string, object>? style);

    void RegisterThemeProvider(IThemeProvider provider);

    event Action<ITheme, bool>? ThemeChanged;
}