using System.Collections.Concurrent;
using TreeSitterSharp.Parser;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp.Scheduler;

public sealed class TaskResult
{
    private readonly string? fileName;
    private readonly uint[]? pathId;
    private readonly string? fileContent;

    public string FilePath
    {
        get
        {
            if (fileName != null && pathId != null)
                return Path.Combine(PathConvert.GetPath(pathId), fileName);
            else
                return "SingleFile";
        }
    }

    public string FileContent
    {
        get => fileContent ?? File.ReadAllText(FilePath);
    }

    public string Language { get; }
    public Guid Id { get; }

    public IEnumerable<Token> Tokens { get; }

    internal TaskResult(string text, IEnumerable<Token> tokens, bool isPath, string lang, Guid id)
    {
        if (isPath)
            (pathId, fileName) = PathConvert.ConvertPath(text);
        else
            fileContent = text;
        Tokens = tokens;
        Language = lang;
        Id = id;
    }
}

internal sealed class TaskProcessor
{
    // 插件机制, 用于自定义解析过程的行为
    private readonly ConcurrentBag<IParseProcessor> _customProcessors = new();

    private readonly ParseCodeToToken _scheduler;

    private readonly TreeSitter treeSitter = new();

    public TaskProcessor(ParseCodeToToken scheduler)
    {
        _scheduler = scheduler;
    }

    public void RegisterProcessor(IParseProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _customProcessors.Add(processor);
    }

    // 此处最好在内部初始化一个变量, 而不是在外部
    public Task Process(ParsingTask task, CancellationToken cancellationToken)
    {
        if (CheckIsCancelled(task, cancellationToken))
            return Task.CompletedTask;

        foreach (var item in _customProcessors)
        {
            item.BeforeParse();
        }

        bool isPath = false;
        string lang = string.Empty;
        ParsingInput input = task.Input;

        if (input.FilePath is string path)
        {
            isPath = true;
            lang = path.Split('.').LastOrDefault()?.ToLower() ?? input.Language;
        }
        else if (input.TextContent != null && input.Language != null)
        {
            lang = input.Language;
        }

        if (string.IsNullOrEmpty(lang))
        {
            task.SetFailed(new Exception("No language specified."));
            return Task.CompletedTask;
        }

        if (CheckIsCancelled(task, cancellationToken))
            return Task.CompletedTask;

        IQueryTree queryTree;
        foreach (var item in _customProcessors)
        {
            item.StartParse();
        }

        if (isPath)
        {
            queryTree = treeSitter.Parser.ParseFile(input.FilePath, lang);
        }
        else
        {
            (queryTree, _) = treeSitter.Parser.ParseString(input.TextContent, lang);
        }

        if (CheckIsCancelled(task, cancellationToken))
            return Task.CompletedTask;

        if (!GetTokens(queryTree, task, out var tokens, out var ex))
        {
            task.SetFailed(new Exception("Failed to get tokens.", ex));
        }

        _scheduler.codeTokens[task.TaskQueueId].Add(
            new(input.FilePath ?? input.TextContent, tokens, isPath, lang, task.Id));

        foreach (var item in _customProcessors)
        {
            item.EndParse();
        }
        task.SetCompleted();
        return Task.CompletedTask;
    }

    private bool GetTokens(
        IQueryTree queryTree,
        ParsingTask task,
        out IEnumerable<Token> tokens,
        out Exception? ex)
    {
        try
        {
            using var sr = new FileStream(
                GetQueryCodePath(queryTree.Language),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            tokens = queryTree.Query(sr, false);
            ex = null;
            return tokens != null;
        }
        catch (Exception exp)
        {
            tokens = [];
            ex = exp;
            return false;
        }
    }

    private string GetQueryCodePath(string language)
        => Path.Combine(_scheduler.Option.Config.QueryDir, language, "codeinfo.scm");

    private bool CheckIsCancelled(ParsingTask task, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            task.Status = TaskStatus.Paused;
            return true;
        }
        return false;
    }
}