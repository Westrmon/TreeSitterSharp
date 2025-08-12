using BitFaster.Caching;
using GitHub.TreeSitter;
using Microsoft.Extensions.ObjectPool;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp.Parser;

internal class Language : ILanguage
{
    private delegate IntPtr LanguageFuncPtr();

    private string _langName = string.Empty;
    private IntPtr _funcHandle;

    private TSLanguage? _language;

    private ParserOption _option;
    private static readonly DefaultObjectPool<IParserFile> _pool;
    private readonly ConcurrentDictionary<string, QueryTree> _treeCache = new();

    public int Count => _treeCache.Count;

    static Language()
    {
        _pool = new DefaultObjectPool<IParserFile>(new ParserPooledObjectPolicy());
    }

    // 在此处拼接出解析器的路径
    public void Init(string lang)
    {
        _langName = lang;
        _option = ParserOption.GetInstance();
        _funcHandle = LoadParse.Load(_langName, _option.Config.ParserDir);
        var langPtr = Marshal.GetDelegateForFunctionPointer<LanguageFuncPtr>(_funcHandle).Invoke();
        if (langPtr != IntPtr.Zero)
            _language = new TSLanguage(langPtr);
        else
            throw new DllNotFoundException($"Failed to load language grammar for '{lang}'. Ensure the parser DLL exists and is correct.");
    }

    // 主要起到一个中间站的作用, 传入langPtr
    public IQueryTree ParseFile(string filePath)
    {
        string content = File.ReadAllText(filePath);
        return _treeCache.AddOrUpdate(filePath,
            (path) => CreateNewQueryTree(path, content),
            (path, existingTree) =>
            {
                UpdateQueryTree(existingTree, content);
                return existingTree;
            });
    }

    public IQueryTree ParseStream(string filePath, StreamReader reader)
        => CreateNewQueryTree(filePath, reader.ReadToEnd());

    public IQueryTree ParseString(string filePath, string codeString)
        => CreateNewQueryTree(filePath, codeString);

    private QueryTree CreateNewQueryTree(string path, string code, bool cacheable = true)
    {
        if (_language == null)
            throw new InvalidOperationException("Language is not initialized");
        var parser = _pool.Get();
        try
        {
            var tree = parser.ParseFile(code, _language);
            var data = new ParserTree(path, tree);
            var queryTree = new QueryTree(_language, data, _langName);

            if (cacheable)
            {
                queryTree.DisposeTree += (item) =>
                    _treeCache.TryRemove(item.Data.FilePath, out _); ;
            }
            return queryTree;
        }
        finally
        {
            _pool.Return(parser);
        }
    }

    private void UpdateQueryTree(QueryTree queryTree, string newCode)
    {
        var parser = _pool.Get();
        try
        {
            parser.ParseFile(newCode, queryTree.Data.Tree);
        }
        finally
        {
            _pool.Return(parser);
        }
    }

    public IQueryTree? GetQuery(string filePath)
        => _treeCache.TryGetValue(filePath, out var tree) ? tree : null;

    public void Dispose()
    {
        LoadParse.UnLoad(_funcHandle);
        foreach (var item in _treeCache.Values)
            item.Dispose();
        _treeCache.Clear();
        _language?.DisposeTokenTable();
    }

    // 引入函数, 在最后退出函数
    private class LoadParse
    {
        internal static IntPtr Load(string lang, string rootDirPath)
        {
            string funcName = "tree_sitter_" + lang.Replace('-', '_');
            string dllPath = Path.Combine(rootDirPath, lang + Path.GetExtension(LoadPath.DLL_PATH));
            if (!File.Exists(dllPath))
                throw new FileNotFoundException("Failed to find parser for " + lang, dllPath);
            if (NativeLibrary.TryLoad(dllPath, out IntPtr handle) &&
               NativeLibrary.TryGetExport(handle, funcName, out IntPtr funcPtr))
            {
                return funcPtr;
            }
            return IntPtr.Zero;
        }

        internal static void UnLoad(IntPtr dllHandle)
        {
            if (dllHandle != IntPtr.Zero)
                NativeLibrary.Free(dllHandle);
        }
    }
}