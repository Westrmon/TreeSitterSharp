using System.Diagnostics;

namespace TreeSitterSharp.Utils;

// 没用上暂时
internal class Log
{
    internal bool IsLog { get; init; }
    private static Log? _instance;
    private static readonly Lock _lock = new();

    private Log()
    { }

    public static Log GetInstance()
    {
        using (_lock.EnterScope())
        {
            _instance ??= new Log();
        }
        return _instance;
    }

    public static void SetLogAction(Action<LogType, Exception?, string?>? action)
    {
        if (action is not null)
        {
            if (_instance is null)
                GetInstance();
            _instance!.LogAction = action;
        }
    }

    public Action<LogType, Exception?, string?>? LogAction { get; set; }
        = (t, e, m) => Debug.WriteLine($"[{t}]{m} | {e?.Message}");

    internal void LogMessage(LogType type, Exception? ex, string message)
        => LogAction?.Invoke(type, ex, message);

    internal void LogMessage(LogType type, string message)
        => LogAction?.Invoke(type, null, message);

    internal void LogError(Exception ex, string? message)
        => LogAction?.Invoke(LogType.Error, ex, message);

    internal void LogError(string message)
        => LogAction?.Invoke(LogType.Error, null, message);

    internal void LogWarning(string message)
        => LogAction?.Invoke(LogType.Warning, null, message);

    internal void LogInfo(string message)
        => LogAction?.Invoke(LogType.Info, null, message);
}