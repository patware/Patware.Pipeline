namespace Pipeline.Runtime;

/// <summary>
/// Captures a step's persisted attempts, arguments, output, polling schedule, and ownership lease.
/// </summary>
/// <remarks>Output presence is independent of its value. A producer can succeed with a serialized JSON null. Lease tokens identify ownership, not attempt numbers.</remarks>
/// <seealso cref="Pipeline.Core.StepDefinition" />
/// <seealso cref="StepClaim" />
public sealed record StepExecutionState
{
    /// <summary>
    /// Gets the identifier of the pipeline run owning this state or work.
    /// </summary>
    public required Guid RunId { get; init; }
    /// <summary>
    /// Gets the job identifier, scoped to its pipeline plan or run.
    /// </summary>
    public required string JobId { get; init; }
    /// <summary>
    /// Gets the step identifier, scoped to its containing job.
    /// </summary>
    public required string StepId { get; init; }

    /// <summary>
    /// Gets the current execution status.
    /// </summary>
    public StepExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the monotonically increasing concurrency revision used to reject stale writes.
    /// </summary>
    public long Revision { get; init; }
    /// <summary>
    /// Gets the number of claimed invocation attempts, initially zero.
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// Gets the JSON array of bound method arguments, excluding the runtime-supplied cancellation token.
    /// </summary>
    public string? ArgumentsJson { get; init; }

    /// <summary>
    /// Gets whether a result was produced; true can accompany a serialized JSON null value.
    /// </summary>
    public bool HasOutput { get; init; }
    /// <summary>
    /// Gets the serialized producer result, or null when no serialized output is available.
    /// </summary>
    public string? OutputJson { get; init; }

    /// <summary>
    /// Gets the start timestamp, or null before execution begins.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }
    /// <summary>
    /// Gets the terminal timestamp, or null while unfinished.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }
    /// <summary>
    /// Gets the scheduled polling wake-up timestamp, or null when no wake-up is scheduled.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; init; }
    /// <summary>
    /// Gets the absolute polling timeout timestamp, or null when no polling deadline is set.
    /// </summary>
    public DateTimeOffset? PollDeadline { get; init; }

    /// <summary>
    /// Gets the ownership token that fences results and logs from stale step invocations.
    /// </summary>
    public Guid? LeaseToken { get; init; }
    /// <summary>
    /// Gets the timestamp after which the step ownership lease is no longer valid.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; init; }

    /// <summary>
    /// Gets the recorded failure details, or null when no error has been recorded.
    /// </summary>
    public string? Error { get; init; }
}