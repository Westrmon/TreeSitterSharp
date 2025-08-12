namespace TreeSitterSharp.Scheduler;

public record ParsingInput(
    string? FilePath = null,
    string? TextContent = null,
    string? Language = null);

public enum TaskPriority
{
    Low = 3,
    Normal = 2,
    High = 1,
    Urgent = 0
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Paused,
}

public sealed class ParsingTask
{
    public Guid TaskQueueId { get; }
    public Guid Id { get; } = Guid.NewGuid();
    public ParsingInput Input { get; }
    public TaskPriority Priority { get; }
    public TaskStatus Status { get; internal set; } = TaskStatus.Pending;

    private readonly TaskCompletionSource<bool> _taskCompletionSource = new();

    public Task<bool> CompletionTask => _taskCompletionSource.Task;

    public ParsingTask(ParsingInput input, TaskPriority priority, Guid taskQueueId)
    {
        Input = input;
        Priority = priority;
        TaskQueueId = taskQueueId;
    }

    internal void SetCompleted()
    {
        Status = TaskStatus.Completed;
        _taskCompletionSource.TrySetResult(true);
    }

    internal void SetFailed(Exception ex)
    {
        Status = TaskStatus.Failed;
        _taskCompletionSource.TrySetException(ex);
    }
}