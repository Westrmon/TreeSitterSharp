namespace TreeSitterSharp.Parser;

internal interface ILanguage : IDisposable
{
    /// <summary>
    /// 获取语言下的语法树的数目
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="lang">语言名</param>
    void Init(string lang);

    /// <summary>
    /// 每次解析就获取一个实例, 支持多线程
    /// </summary>
    /// <param name="filePath">解析的文件路径, 最好为相对路径</param>
    /// <returns></returns>
    IQueryTree ParseFile(string filePath);

    IQueryTree ParseStream(string virtualPath, StreamReader reader);

    IQueryTree ParseString(string virtualPath, string code);

    IQueryTree? GetQuery(string filePath);
}