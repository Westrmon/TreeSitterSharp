using BitFaster.Caching.Lru;
using GitHub.TreeSitter;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp.Parser;

internal class QueryTree : IQueryTree
{
    private TSQuery? _query;
    private readonly ParserTree _tree;
    private readonly TSLanguage _language;
    private Dictionary<int, string> _tokenKindTable;
    private bool _profile = true;
    private static readonly ConcurrentLru<int, TSQuery> _queryCache;

    internal ParserTree Data => _tree;

    // 当多个一个token被多个种匹配的时候就会使用此表来筛选优化级别最高的一项, 后续需要改造, 使用配置文件
    private static readonly Dictionary<string, ushort> _kindProfile = new()
    {
        { "comment", 50 },
        { "operator", 55 },
        { "string", 60 },
        { "variable", 65 },
        { "parameter", 70 },
        { "constant", 75 },
        { "function", 80 },
        { "namespace", 85 },
        { "punctuation", 90 },
        { "type", 95 },
        { "keyword", 100 },
    };

    public string Language { get; }

    internal QueryTree(TSLanguage lang, ParserTree tree, string langName)
    {
        _language = lang;
        _tree = tree;
        Language = langName;
    }

    static QueryTree()
    {
        var config = ParserOption.GetInstance().Config;
        _queryCache = new ConcurrentLru<int, TSQuery>(config.MaxQueryTreeCacheSize);
    }

    internal event Action<QueryTree> DisposeTree;

    public bool IsEqual(string filePath)
        => _tree.FilePath == filePath;

    public bool IsEqual(ushort[] path, string fileName)
        => _tree.IsEqual(path, fileName);

    public IEnumerable<Token> Query(string queryCode, bool profile)
    {
        _profile = profile;
        _query ??= _queryCache.GetOrAdd(queryCode.GetHashCode(), key =>
            {
                var q = _language.CreateQuery(
                    queryCode,
                    key,
                    out var errOffset,
                    out var errType,
                    out _tokenKindTable);
                CheckError(q, errOffset, errType);
                return q;
            });
        return Query();
    }

    public IEnumerable<Token> Query(FileStream queryStream, bool profile)
    {
        _profile = profile;
        var hash = queryStream.Name.GetHashCode();
        _query ??= _queryCache.GetOrAdd(hash, key =>
        {
            var q = _language.CreateQuery(
                queryStream,
                key,
                out var errOffset,
                out var errType,
                out _tokenKindTable);
            CheckError(q, errOffset, errType);
            return q;
        });
        if (_tokenKindTable == null && !_language.TryGetTokenTable(hash, out _tokenKindTable))
            throw new Exception("Failed to get token table");

        return Query();
    }

    private IEnumerable<Token> Query()
    {
        bool isOverlapping = false;
        (ushort PatternIndex, ushort BeforeIndex, uint Start, uint End, uint OriginalEnd)? previousMatch = null;
        Dictionary<uint, Token> parsedTokens = new();
        using var cursor = new TSQueryCursor();
        cursor.exec(_query, _tree.Tree.root_node());
        while (cursor.next_match(out var match, out var captures))
        {
            if (match.capture_count == 0
             || captures?.FirstOrDefault().node is not { } captureNode)
                continue;

            var (start, end) = (captureNode.start_offset(), captureNode.end_offset());
            if (previousMatch is null)
            {
                previousMatch = (match.pattern_index, match.pattern_index, start, end, end);
                continue;
            }
            var current = previousMatch.Value;
            var (currentIndex, endIndex) = (match.pattern_index, end);
            if (current.Start != start || !_profile)
            {
                string tokenType = GetTokenType(current.PatternIndex, current.BeforeIndex, isOverlapping);
                UpsertToken(parsedTokens, current.Start, current.OriginalEnd, current.End, tokenType);
                isOverlapping = false;
            }
            (isOverlapping, endIndex) = CheckOverlap(current.End, end);
            previousMatch = (
                    currentIndex,
                    isOverlapping ? previousMatch.Value.BeforeIndex : currentIndex,
                    start,
                    endIndex,
                    end);
        }

        if (previousMatch is { } lastMatch)
        {
            var tokenType = GetTokenType(lastMatch.PatternIndex, lastMatch.BeforeIndex, false);
            UpsertToken(parsedTokens, lastMatch.Start, lastMatch.OriginalEnd, lastMatch.End, tokenType);
        }

        return parsedTokens.Select(e => e.Value);
    }

    private void UpsertToken(
        Dictionary<uint, Token> tokens,
        uint start,
        uint originalEnd,
        uint end,
        string tokenType)
    {
        if (tokens.TryGetValue(start, out var existing))
        {
            if (!existing.IsEqual(start, end, tokenType))
                tokens[start] = CreateAdjustedToken(existing, tokenType);
        }
        else
        {
            tokens.Add(start, new Token(start, originalEnd, tokenType));
        }
    }

    private string GetTokenType(ushort patternIndex, ushort beforeIndex, bool isOverlapping)
            => isOverlapping ? ComparerIndex(patternIndex, beforeIndex) : _tokenKindTable[patternIndex];

    private Token CreateAdjustedToken(Token original, string newType)
        => new(original.StartOffset, original.EndOffset, ComparerIndex(original.TokenKind, newType));

    private static (bool IsOverlapping, uint EndIndex) CheckOverlap(uint previousEnd, uint currentEnd)
        => previousEnd >= currentEnd ? (true, previousEnd) : (false, currentEnd);

    private string ComparerIndex(ushort index, ushort befIndex)
    {
        string orgin = _tokenKindTable[index];
        string bef = _tokenKindTable[befIndex];
        return ComparerIndex(orgin, bef);
    }

    public string ComparerIndex(string preKind, string kind)
    {
        string orgin = preKind.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        string bef = kind.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        if (_profile)
            return _kindProfile[orgin] > _kindProfile[bef] ? orgin : bef;
        else
            return preKind;
    }

    private static void CheckError(TSQuery q, uint errOffset, TSQueryError errType)
    {
        if (q == null)
            throw new Exception($"Cant Create Query: [{errType}] {errOffset}");
    }

    public void Dispose()
    {
        DisposeTree?.Invoke(this);
        _query?.Dispose();
        _tree.Dispose();
    }
}