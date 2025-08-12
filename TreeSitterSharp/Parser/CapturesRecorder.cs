using GitHub.TreeSitter;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace TreeSitterSharp.Parser;

public static partial class CapturesRecorder
{
    private static readonly ConcurrentDictionary<int, Dictionary<int, string>> TokenTableCache = new();

    public static TSQuery CreateQuery(this TSLanguage language, FileStream file, int hashCode, out uint error_offset, out TSQueryError error_type, out Dictionary<int, string> tokenTable)
    {
        var sb = new StringBuilder();
        tokenTable = TokenTableCache.GetOrAdd(hashCode, key =>
        {
            using var reader = new StreamReader(file);
            return GetTokenKindTable(() =>
            {
                var line = reader.ReadLine();
                if (!string.IsNullOrEmpty(line) && !line.StartsWith(';'))
                    sb.AppendLine(line);
                return line ?? "EOF";
            });
        });
        string scmCode = sb.ToString();
        sb.Clear();
        sb = null;
        return language.query_new(scmCode, out error_offset, out error_type);
    }

    public static TSQuery CreateQuery(this TSLanguage language, string scmCode, int hashCode, out uint error_offset, out TSQueryError error_type, out Dictionary<int, string> tokenTable)
    {
        tokenTable = TokenTableCache.GetOrAdd(hashCode, key =>
        {
            var lines = scmCode.Split('\n');
            int lineCount = 0;
            return GetTokenKindTable(() =>
            {
                return lineCount < lines.Length ? lines[lineCount++] : "EOF";
            });
        });
        return language.query_new(scmCode, out error_offset, out error_type);
    }

    public static bool TryGetTokenTable(this TSLanguage language, int hash, out Dictionary<int, string>? tokenTable)
        => TokenTableCache.TryGetValue(hash, out tokenTable);

    public static void DisposeTokenTable(this TSLanguage language, string scmCodeOrFilePath)
    {
        TokenTableCache.TryRemove(scmCodeOrFilePath.GetHashCode(), out _);
    }

    public static void DisposeTokenTable(this TSLanguage language)
    {
        TokenTableCache.Clear();
    }

    private static Dictionary<int, string> GetTokenKindTable(Func<string> lines)
    {
        int patternIndex = 0;
        var regex = CaptureName();
        var tokenTable = new Dictionary<int, string>();
        var stack = new Stack<char>();
        string line;
        while ((line = lines()) != "EOF")
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith(';')) continue;
            if (IsBalanced(line.AsSpan(), stack))
            {
                var matches = regex.Matches(line);
                var match = matches.Count != 0 ? matches.Last() : null;
                if (match != null && match.Success)
                    tokenTable.Add(patternIndex++, match.Groups["CaptureName"].Value);
            }
        }
        return tokenTable;
    }

    private static bool IsBalanced(ReadOnlySpan<char> input, Stack<char> stack)
    {
        // 需要判断 ; 是否是字符串内的符号
        bool isInString = false;
        bool isEscape = false; // 是否转义
        stack ??= new Stack<char>();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(' || input[i] == '[')
            {
                stack.Push(input[i]);
            }
            else if (input[i] == ')' || input[i] == ']')
            {
                if (stack.Count == 0) return false;
                else if (stack.TryPeek(out char bracket) && bracket == (input[i] == ')' ? '(' : '['))
                    stack.Pop();
                else
                    return false;
            }
            else if (input[i] == '\\')
            {
                isEscape = true;
                continue;
            }
            else if (!isEscape && input[i] == '"')
            {
                isInString = !isInString;
            }
            else if (input[i] == ';' && !isInString)
            {
                break;
            }
        }
        return stack.Count == 0;
    }

    [GeneratedRegex(@"@(?<CaptureName>[\w\.]*\b)", RegexOptions.Singleline)]
    private static partial Regex CaptureName();
}