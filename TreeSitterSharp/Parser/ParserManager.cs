using BitFaster.Caching.Lru;
using GitHub.TreeSitter;
using System.Collections.Concurrent;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp.Parser;

// 现在没有一个可以清除久无用的Language实例, 后面需要写一个, 防止内存泄露
internal class ParserManager : IParser
{
    private static Lock lockObj = new Lock();
    private static ParserManager? instance;
    private ConcurrentLru<string, Lazy<ILanguage>> parserCache;
    private ConcurrentBag<int>? streamFile = null;

    public static string ParserRootPath => LoadPath.DLL_PATH;

    private ParserManager()
    {
        var config = ParserOption.GetInstance().Config;
        parserCache ??= new(config.MaxParserTreeCacheSize);
    }

    public static IParser GetInstance()
    {
        using (lockObj.EnterScope())
        {
            instance ??= new ParserManager();
        }
        return instance;
    }

    public IQueryTree ParseFile(string filePath)
        => ParseFile(filePath, Path.GetExtension(filePath));

    public IQueryTree ParseFile(string filePath, string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
            throw new ArgumentNullException(nameof(suffix));

        SuffixTable.TryGetLanguageBySuffix(suffix, out var name);
        var language = GetLanguage(name);
        return language.ParseFile(filePath);
    }

    // 临时解析
    public IQueryTree ParseFile(StreamReader reader, string suffix)
    {
        int hashCode = reader.GetHashCode();
        string tmpPath = Path.Combine("StreamTemp", hashCode.ToString());
        SuffixTable.TryGetLanguageBySuffix(suffix, out var name);
        var language = GetLanguage(name);
        streamFile ??= [];
        streamFile.Add(hashCode);
        return language.ParseStream(tmpPath, reader);
    }

    public (IQueryTree Query, string VirtualPath) ParseString(string code, string langName)
    {
        int hashCode = code.GetHashCode();
        string tmpPath = Path.Combine("ST", hashCode.ToString());
        var lang = GetLanguage(langName);
        streamFile ??= [];
        streamFile.Add(hashCode);
        return (lang.ParseString(tmpPath, code), tmpPath);
    }

    public IQueryTree? GetQuery(string filePath)
        => GetQuery(filePath, Path.GetExtension(filePath));

    public IQueryTree? GetQuery(string filePath, string suffix)
    {
        SuffixTable.TryGetLanguageBySuffix(suffix, out var name);
        if (parserCache.TryGet(name, out var language))
            return language.Value.GetQuery(filePath);
        return null;
    }

    public IQueryTree? GetQuery(int hashCode)
    {
        string tmpPath = Path.Combine("StreamTemp", hashCode.ToString());
        if (parserCache.TryGet(hashCode.ToString(), out var language))
            return language.Value.GetQuery(tmpPath);
        return null;
    }

    public void DisposeLanguage(string suffix)
    {
        SuffixTable.TryGetLanguageBySuffix(suffix, out var name);
        if (parserCache.TryGet(name, out var language) && language.IsValueCreated)
            language.Value.Dispose();
    }

    public void Dispose()
    {
        parserCache?.Clear();
    }

    private ILanguage GetLanguage(string name)
    {
        var lazyLanguage = parserCache.GetOrAdd(name, key => new Lazy<ILanguage>(() =>
        {
            var lang = new Language();
            lang.Init(name);
            return lang;
        }, LazyThreadSafetyMode.ExecutionAndPublication));

        return lazyLanguage.Value;
    }
}