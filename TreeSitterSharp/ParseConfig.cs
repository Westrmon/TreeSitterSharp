namespace TreeSitterSharp;

// 这里主要是提供一个方法, 内部通过此类来获取配置信息, 但是不管配置信息存在哪里
public class ParseConfig
{
    public int MaxConcurrentTasks { get; set; }
        = 4;

    public string ParserDir { get; set; }
        = Path.Combine(Environment.CurrentDirectory, "Resources", "Parsers");

    public string QueryDir { get; set; }
        = Path.Combine(Environment.CurrentDirectory, "Resources", "Queries");

    public int MaxQueryTreeCacheSize { get; set; } = 1000;

    public int MaxParserTreeCacheSize { get; set; } = 1000;
}