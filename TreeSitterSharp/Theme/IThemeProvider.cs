namespace TreeSitterSharp.Theme;

// 用于自定义读取主题文件, 库通过此接口获取指定的颜色
public interface IThemeProvider
{
    bool IsDarkMode { get; set; }

    // 主要作用就是将非范围内的标记直接抛弃, 在一定程度上减少了内存占用?
    void AddExternConfigKey(string key);

    // 主题文件的结构必须是由标记和样式组成的键值对
    bool GetConfigByMark(string mark, out Dictionary<string, object>? style);

    void SetDefaultTheme();

    event Action? OnThemePropertyChanged;
}