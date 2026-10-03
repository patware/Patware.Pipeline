namespace Pipeline.Runtime;

/// <summary>
/// Captures a job's persisted status, condition result, revision, and timing for one run.
/// </summary>
/// <seealso cref="Pipeline.Core.JobDefinition" />
/// <seealso cref="JobExecutionStatus" />
public sealed record JobExecutionState
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
    /// Gets the current execution status.
    /// </summary>
    public JobExecutionStatus Status { get; init; }
    /// <summary>
    /// Gets the monotonically increasing concurrency revision used to reject stale writes.
    /// </summary>
    public long Revision { get; init; }

    /// <summary>
    /// Gets the evaluated job condition, or null when no result has been recorded.
    /// </summary>
    public bool? ConditionResult { get; init; }

    /// <summary>
    /// Gets the start timestamp, or null before execution begins.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }
    /// <summary>
    /// Gets the terminal timestamp, or null while unfinished.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>
    /// Gets the recorded failure details, or null when no error has been recorded.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Groups run metadata with job and step state for one consistent execution view.
/// </summary>
/// <seealso cref="IPipelineExecutionStore" />
/// <seealso cref="IPipelineRuntime.GetExecutionAsync" />
/// <param name="Run">The run metadata and ordered logs associated with this execution snapshot.</param>
/// <param name="Jobs">The jobs belonging to this plan or execution snapshot.</param>
/// <param name="Steps">The steps belonging to this job or execution snapshot.</param>
public sealed record PipelineExecutionSnapshot(
    PipelineRun Run,
    IReadOnlyList<JobExecutionState> Jobs,
    IReadOnlyList<StepExecutionState> Steps);