namespace TreeSitterSharp.Parser;

// 主要和language和查询的代码有关, 其中查询代码需要缓存一个索引列表
public interface IQueryTree : IDisposable
{
    string Language { get; }

    bool IsEqual(string filePath);

    bool IsEqual(ushort[] path, string fileName);

    /// <summary>
    /// 根据查询代码获取查询结果
    /// </summary>
    /// <param name="queryCode">查询语句</param>
    /// <param name="profile">是否需要复杂查询, 如果为false, 那么将会跳过重复检查</param>
    /// <returns></returns>
    IEnumerable<Token> Query(string queryCode, bool profile = true);

    IEnumerable<Token> Query(FileStream queryStream, bool profile = true);
}