using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Describes the lifecycle of a pipeline run from creation through completion or failure.
/// </summary>
public enum PipelineStatus
{
    /// <summary>
    /// The initial default state before a run is queued.
    /// </summary>
    init,
    /// <summary>
    /// Persisted and awaiting processing.
    /// </summary>
    Queued,
    /// <summary>
    /// Execution has started and has not reached a terminal outcome.
    /// </summary>
    Running,
    /// <summary>
    /// All jobs succeeded or were skipped by their conditions.
    /// </summary>
    Completed,
    /// <summary>
    /// Ended unsuccessfully because execution or coordination failed.
    /// </summary>
    Failed,
    /// <summary>
    /// Execution was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Captures one submitted run's identity, lifecycle metadata, revision, and ordered log history.
/// </summary>
/// <seealso cref="IPipelineRuntime" />
/// <seealso cref="PipelineExecutionSnapshot" />
public sealed record PipelineRun
{
    /// <summary>
    /// Gets the globally unique run identifier assigned at submission.
    /// </summary>
    public required Guid Id { get; init; }
    /// <summary>
    /// Gets the versioned pipeline definition associated with this plan or run.
    /// </summary>
    public required PipelineDefinition Definition { get; init; }
    /// <summary>
    /// Gets the user-facing title of this particular run.
    /// </summary>
    public required string Title { get; init; }
    /// <summary>
    /// Gets the caller-supplied identity of the person or service that submitted the run.
    /// </summary>
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Gets the monotonically increasing concurrency revision used to reject stale writes.
    /// </summary>
    public long Revision { get; init; }

    /// <summary>
    /// Gets the timestamp at which the runtime created the run.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>
    /// Gets the timestamp at which the run was submitted for processing.
    /// </summary>
    public required DateTimeOffset QueuedAt { get; init; }
    /// <summary>
    /// Gets the start timestamp, or null before execution begins.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }
    /// <summary>
    /// Gets the terminal timestamp, or null while unfinished.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>
    /// Gets the current execution status.
    /// </summary>
    public PipelineStatus Status { get; init; }
    /// <summary>
    /// Gets the human-readable explanation of the current run status.
    /// </summary>
    public string StatusText { get; init; } = "";

    /// <summary>
    /// Gets the ordered, append-only log history for the run.
    /// </summary>
    public IReadOnlyList<PipelineLogEntry> Logs { get; init; } = [];

}
