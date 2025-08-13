using System.Diagnostics;
using TreeSitterSharp;
using TreeSitterSharp.Scheduler;
using TreeSitterSharp.Utils;
using Xunit.Abstractions;

namespace Test;

public class UnitTest1
{
    private readonly ITestOutputHelper output;

    public UnitTest1(ITestOutputHelper output)
    {
        this.output = output;
    }

    // 每一个工作单元需要执行的内容
    private async Task CpuIntensiveParseFile(ParsingTask task, CancellationToken cancellationToken)
    {
        output.WriteLine(
            $"\t[Thread: {Thread.CurrentThread.ManagedThreadId}] -> STARTING CPU work for '{task.Input.FilePath}' ({task.Priority})."
        );

        // Simulate heavy CPU work instead of waiting
        long total = 0;
        for (int i = 0; i < 2_000_000_000; i++)
        {
            total += 1;
        }

        // A small delay to ensure output doesn't get too jumbled
        await Task.Delay(10, cancellationToken);

        output.WriteLine(
            $"\t[Thread: {Thread.CurrentThread.ManagedThreadId}  QueueId: {task.TaskQueueId}] <- FINISHED CPU work for '{task.Input.FilePath}'."
        );
    }

    private string TestCode = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace TreeSitterSharp;
public class LanguageDetector
{
public LanguageDetector()
   {
   }
public static void Main(int argc, string[] args)
   {
       Console.WriteLine(""Hello World!\"");
   }
}";

    private CancellationToken InitPriorityTaskScheduler(ParseCodeToToken scheduler)
    {
        var cancellationToken = new CancellationTokenSource();
        scheduler.OnLog += (logMessage) => { output.WriteLine(logMessage); };
        scheduler.OnQueueIdle += cancellationToken.Cancel;
        return cancellationToken.Token;
    }

    private void InitTreeSitter()
    {
        ParserOption option = ParserOption.GetInstance();
        var config = new ParseConfig();
        config.MaxConcurrentTasks = 8;
        config.MaxQueryTreeCacheSize = 260;
        config.ParserDir = @"D:\Code\Programs\CodeReader\CodeReader\bin\Debug\net9.0\win-x64\Resources\Parsers";
        config.QueryDir = @"D:\Code\Programs\CodeReader\CodeReader\bin\Debug\net9.0\win-x64\Resources\Queries";
        option.ProjectDir = @"D:\Desktop\src";
        option.SetConfig(config);
    }

    private void PlantSingleResult(ParseCodeToToken scheduler, Guid id)
    {
        if (!scheduler.GetResults(id, out var results))
            throw new Exception("没有创建任务队列");
        foreach (var result in results)
        {
            foreach (var token in result.Tokens)
            {
                var codeToken = TestCode.AsSpan((int)token.StartOffset, (int)token.Length);
                if (token.TokenKind.EndsWith("range"))
                    continue;
                output.WriteLine($"[Token]: {codeToken}\t[Kind]: {token.TokenKind}");
            }
        }
    }

    private void PlantResult(ParseCodeToToken scheduler, Guid id)
    {
        if (!scheduler.GetResults(id, out var results))
            throw new Exception("没有创建任务队列");
        foreach (var result in results)
        {
            string content = File.ReadAllText(result.FilePath);
            output.WriteLine($"--- {result.FilePath} ---");
            foreach (var token in result.Tokens)
            {
                var codeToken = content.AsSpan((int)token.StartOffset, (int)token.Length);
                if (token.TokenKind.EndsWith("range"))
                    continue;
                output.WriteLine($"[Token]: {codeToken}\t[Kind]: {token.TokenKind}");
            }
        }
    }

    [Fact]
    public async Task SchedulersTest()
    {
        output.WriteLine("--- Multithreading Proof ---");
        // Use 4 workers and our new CPU-intensive function
        using var scheduler = new ParseCodeToToken(CpuIntensiveParseFile, 2);
        var cancellationToken = InitPriorityTaskScheduler(scheduler);

        var id = scheduler.CreateTaskQueue();
        var id2 = scheduler.CreateTaskQueue();
        output.WriteLine("Enqueuing 4 tasks...");
        scheduler.AddTask(id, new ParsingInput(FilePath: "Task A"), TaskPriority.Low);
        scheduler.AddTask(id, new ParsingInput(FilePath: "Task B"), TaskPriority.Normal);
        scheduler.AddTask(id, new ParsingInput(FilePath: "Task C"), TaskPriority.Normal);
        scheduler.AddTask(id2, new ParsingInput(FilePath: "Task D"), TaskPriority.High);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }
    }

    [Fact]
    public async Task SingleParseTest()
    {
        InitTreeSitter();

        // 核心调用
        using var scheduler = new ParseCodeToToken();
        var cancellationToken = InitPriorityTaskScheduler(scheduler);
        var id = scheduler.CreateTaskQueue();
        scheduler.AddTask(id, new ParsingInput(null, TestCode, "cs"));
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
        PlantSingleResult(scheduler, id);
    }

    private int idg = 0;

    [Fact]
    public async Task MultiParseTest()
    {
        var locks = new Lock();
        InitTreeSitter();
        using var scheduler = new ParseCodeToToken();
        var cancellationToken = InitPriorityTaskScheduler(scheduler);
        var id = scheduler.CreateTaskQueue();
        string testFileDir = @"D:\Desktop\src";

        scheduler.OnTaskCompleted += task =>
        {
            Interlocked.Increment(ref idg);
            Debug.WriteLine($"已完成: {idg}");
        };
        foreach (var file in Directory.GetFiles(testFileDir, "*.cs", SearchOption.AllDirectories))
        {
            while (scheduler.TotalIncompleteTasks > 500) ;
            scheduler.AddTask(id, new ParsingInput(file));
        }
        try
        {
            await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
        PlantResult(scheduler, id);
        var s = scheduler.TaskTokens[id].Select(p => p.Tokens.Count());
    }
}
