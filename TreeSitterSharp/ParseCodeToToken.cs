using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using TreeSitterSharp.Scheduler;
using TreeSitterSharp.Utils;
using TaskStatus = TreeSitterSharp.Scheduler.TaskStatus;

namespace TreeSitterSharp;

public sealed class ParseCodeToToken : IDisposable
{
    private readonly Lock _queueLock = new();

    private int _totalIncompleteTasks = 0;
    private readonly PriorityQueue<ParsingTask, TaskPriority> _pendingTasks = new();
    private readonly ConcurrentDictionary<Guid, ParsingTask> _runningTasks = new();
    private readonly ConcurrentDictionary<Guid, int> _taskCount = new();

    private readonly SemaphoreSlim _concurrencySemaphore;

    private readonly SemaphoreSlim _tasksAvailableSemaphore = new(0); // Signals when a task is in the queue
    private readonly Func<ParsingTask, CancellationToken, Task> _taskProcessor;

    private readonly CancellationTokenSource _shutdownCts = new();

    private readonly Task _dispatcherTask;

    internal readonly ConcurrentDictionary<Guid, List<TaskResult>> codeTokens = new();

    // --- Events for Signaling ---
    /// <summary>
    /// Fired when a task is completed
    /// </summary>
    public event Action<ParsingTask>? OnTaskCompleted;

    /// <summary>
    /// 当一个队列的任务完成的时候触发
    /// </summary>
    public event Action<Guid>? OnTaskQueueCompleted;

    /// <summary>
    /// Fired when a task is failed
    /// </summary>
    public event Action<ParsingTask, Exception>? OnTaskFailed;

    /// <summary>
    /// Fired when all tasks are complete
    /// </summary>
    public event Action? OnQueueIdle;

    // For test
    public event Action<string>? OnLog;

    public int TotalIncompleteTasks => _totalIncompleteTasks;

    public ParserOption Option { get; } = ParserOption.GetInstance();

    public IReadOnlyDictionary<Guid, IEnumerable<TaskResult>> TaskTokens
        => codeTokens.ToImmutableDictionary(
            kv => kv.Key,
            kv => kv.Value.AsEnumerable());

    public Action<IParseProcessor> RegisterProcessor { get; }

    // For test
    internal ParseCodeToToken(Func<ParsingTask, CancellationToken, Task> taskProcessor, int maxConcurrentTasks = 4)
    {
        Debug.Assert(maxConcurrentTasks >= 1, nameof(maxConcurrentTasks), "Must be at least 1.");

        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
        _taskProcessor = taskProcessor;

        // Start the background task dispatcher
        _dispatcherTask = Task.Run(DispatchLoopAsync);
    }

    public ParseCodeToToken()
    {
        var maxConcurrentTasks = Option.Config.MaxConcurrentTasks;
        Debug.Assert(maxConcurrentTasks >= 1, nameof(maxConcurrentTasks), "Must be at least 1.");

        _concurrencySemaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
        var processor = new TaskProcessor(this);
        _taskProcessor = processor.Process;
        RegisterProcessor = processor.RegisterProcessor;
        // Start the background task dispatcher
        _dispatcherTask = Task.Run(DispatchLoopAsync);
    }

    public Guid CreateTaskQueue()
    {
        var guid = Guid.NewGuid();
        codeTokens.TryAdd(guid, []);
        return guid;
    }

    /// <summary>
    /// Adds a new parsing task to the queue.
    /// The task will be executed according to its priority when a worker becomes available.
    /// </summary>
    public ParsingTask AddTask(
        Guid queueId,
        ParsingInput input,
        TaskPriority priority = TaskPriority.Normal)
    {
        var task = new ParsingTask(input, priority, queueId);
#if DEBUG
        OnLog?.Invoke($"[Enqueue] New task {task.Id} with priority {priority}.");
#endif

        using (_queueLock.EnterScope())
        {
            _taskCount.AddOrUpdate(queueId, 1, (k, v) => v + 1);
            _pendingTasks.Enqueue(task, task.Priority);
            Interlocked.Increment(ref _totalIncompleteTasks);
        }

        // Signal the dispatcher that a new task is ready to be processed.
        _tasksAvailableSemaphore.Release();

        return task;
    }

    public bool GetResults(Guid queueId, out IEnumerable<TaskResult> results)
        => TaskTokens.TryGetValue(queueId, out results);

    public bool GetResults(ParsingTask task, out TaskResult? results)
    {
        bool result = codeTokens.TryGetValue(task.TaskQueueId, out var resultsList);
        results = resultsList?.FirstOrDefault(r => r.Id == task.Id);
        return result;
    }

    /// <summary>
    /// The main loop that waits for tasks and available slots, then dispatches them.
    /// This is an efficient event-driven loop, not a polling loop.
    /// </summary>
    private async Task DispatchLoopAsync()
    {
        while (!_shutdownCts.IsCancellationRequested)
        {
            try
            {
                await _tasksAvailableSemaphore.WaitAsync(_shutdownCts.Token);
                await _concurrencySemaphore.WaitAsync(_shutdownCts.Token);

                ParsingTask taskToRun;
                using (_queueLock.EnterScope())
                {
                    _pendingTasks.TryDequeue(out taskToRun, out _);
                }

                if (taskToRun != null)
                {
                    _runningTasks.TryAdd(taskToRun.Id, taskToRun);
                    _ = ExecuteTaskAsync(taskToRun);
                }
                else
                {
                    _concurrencySemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
#if DEBUG
        OnLog?.Invoke("[Dispatcher] Dispatch loop shutting down.");
#endif
    }

    private async Task ExecuteTaskAsync(ParsingTask task)
    {
        task.Status = TaskStatus.Running;
        _taskCount[task.TaskQueueId]--;
#if DEBUG
        OnLog?.Invoke($"[Execute] Starting task {task.Id} ({task.Priority}). Running: {_runningTasks.Count}/{_concurrencySemaphore.CurrentCount + 1}");
#endif
        try
        {
            await _taskProcessor(task, _shutdownCts.Token);

            task.SetCompleted();
            OnTaskCompleted?.Invoke(task);
#if DEBUG
            OnLog?.Invoke($"[Execute] Completed task {task.Id}.");
#endif
        }
        catch (OperationCanceledException)
        {
            task.SetFailed(new TaskCanceledException("Scheduler was shut down."));
#if DEBUG
            OnLog?.Invoke($"[Execute] Canceled task {task.Id} due to shutdown.");
#endif
        }
        catch (Exception ex)
        {
            task.SetFailed(ex);
            OnTaskFailed?.Invoke(task, ex);
#if DEBUG
            OnLog?.Invoke($"[Execute] Failed task {task.Id}: {ex.Message}");
#endif
        }
        finally
        {
            _runningTasks.TryRemove(task.Id, out _);
            _concurrencySemaphore.Release();

            if (_taskCount[task.TaskQueueId] == 0)
            {
#if DEBUG
                OnLog?.Invoke($"[Status] QueueId: {task.TaskQueueId} have been completed. ");
#endif
                OnTaskQueueCompleted?.Invoke(task.TaskQueueId);
            }

            if (Interlocked.Decrement(ref _totalIncompleteTasks) == 0)
            {
#if DEBUG
                OnLog?.Invoke("[Status] All tasks have been completed. Queue is now idle.");
#endif
                OnQueueIdle?.Invoke();
            }
        }
    }

    public void Dispose()
    {
        if (!_shutdownCts.IsCancellationRequested)
        {
            _shutdownCts.Cancel();
        }

        _dispatcherTask.Wait(TimeSpan.FromSeconds(5));

        _shutdownCts.Dispose();
        _concurrencySemaphore.Dispose();
        _tasksAvailableSemaphore.Dispose();
#if DEBUG
        OnLog?.Invoke("[Dispose] Scheduler has been disposed.");
#endif
    }
}