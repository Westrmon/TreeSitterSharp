namespace TreeSitterSharp.Utils;

// 此类需要修改, 实现从文件中加载
public static class SuffixTable
{
    public record LanguageInfo(string Name, IReadOnlyList<string> Suffixes);

    private static readonly LanguageInfo[] s_languages =
    [
        new("c", ["c", "h"]),
        new("cpp", ["cpp", "hpp", "cxx", "cc"]),
        new("c-sharp", ["cs", "csx"]),
        new("go", ["go"]),
        new("java", ["java"]),
        new("javascript", ["js", "mjs", "cjs"]),
        new("yaml", ["yaml", "yml"]),
        new("python", ["py", "pyw"]),
        new("typescript", ["ts", "tsx"]),
        new("sql", ["sql"]),
        new("json", ["json"]),
        new("markdown", ["md", "markdown"])
    ];

    private static readonly IReadOnlyDictionary<string, LanguageInfo> s_bySuffix;
    private static readonly IReadOnlyDictionary<string, LanguageInfo> s_byName;

    static SuffixTable()
    {
        var bySuffix = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in s_languages)
        {
            byName[lang.Name] = lang;
            foreach (var suffix in lang.Suffixes)
            {
                bySuffix[suffix] = lang;
            }
        }

        s_bySuffix = bySuffix;
        s_byName = byName;
    }

    /// <summary>
    /// Gets a collection of all registered languages.
    /// </summary>
    public static IEnumerable<LanguageInfo> AllLanguages => s_languages;

    /// <summary>
    /// Gets a collection of all supported language names.
    /// </summary>
    public static IEnumerable<string> AllLanguageNames => s_byName.Keys;

    /// <summary>
    /// Gets a collection of all supported file suffixes.
    /// </summary>
    public static IEnumerable<string> AllSuffixes => s_bySuffix.Keys;

    /// <summary>
    /// Attempts to find language information based on a file suffix.
    /// This is the preferred method for lookups as it does not throw exceptions.
    /// </summary>
    /// <param name="suffix">The file suffix (without the dot), e.g., "cs" or "py".</param>
    /// <param name="languageInfo">The found LanguageInfo object, or null if not found.</param>
    /// <returns>True if the suffix is supported; otherwise, false.</returns>
    public static bool TryGetLanguageBySuffix(string suffix, out string? language)
    {
        suffix = suffix.ToLower().TrimStart('.');
        bool rs = s_bySuffix.TryGetValue(suffix, out var languageInfo);
        language = languageInfo?.Name;
        return rs;
    }

    /// <summary>
    /// Attempts to find language information based on its canonical name.
    /// </summary>
    /// <param name="languageName">The language name, e.g., "C#" or "Python".</param>
    /// <param name="languageInfo">The found LanguageInfo object, or null if not found.</param>
    /// <returns>True if the language name is supported; otherwise, false.</returns>
    public static bool TryGetLanguageByName(string languageName, out LanguageInfo? languageInfo)
    {
        return s_byName.TryGetValue(languageName, out languageInfo);
    }

    /// <summary>
    /// Checks if a given file suffix is associated with any supported language.
    /// </summary>
    /// <param name="suffix">The file suffix to check.</param>
    /// <returns>True if the suffix is supported; otherwise, false.</returns>
    public static bool IsSupportedSuffix(string suffix)
    {
        return s_bySuffix.ContainsKey(suffix);
    }
}