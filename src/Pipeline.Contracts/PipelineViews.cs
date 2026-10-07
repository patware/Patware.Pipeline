namespace Pipeline.Contracts;

/// <summary>
/// Identifies the definition associated with a displayed pipeline run.
/// </summary>
/// <param name="Id">The stable definition identifier.</param>
/// <param name="DisplayName">The definition's display name.</param>
/// <param name="Version">The definition version used by the run.</param>
public sealed record PipelineDefinitionView(
    string Id,
    string DisplayName,
    int Version = 1);

/// <summary>
/// Represents a displayable pipeline log entry.
/// </summary>
/// <param name="Timestamp">The time the entry was recorded.</param>
/// <param name="Message">The message to display.</param>
public sealed record PipelineLogEntryView(
    DateTimeOffset Timestamp,
    string Message)
{
    /// <summary>
    /// Gets the severity of the entry.
    /// </summary>
    public PipelineLogLevel Level { get; init; }

    /// <summary>
    /// Gets the job scope, or null for a run-level entry.
    /// </summary>
    public string? JobId { get; init; }

    /// <summary>
    /// Gets the step scope, or null when the entry has no step scope.
    /// </summary>
    public string? StepId { get; init; }
}

/// <summary>
/// Contains the metadata and logs needed to display a pipeline run.
/// </summary>
public sealed record PipelineRunView
{
    /// <summary>
    /// Gets the run identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the definition associated with the run.
    /// </summary>
    public required PipelineDefinitionView Definition { get; init; }

    /// <summary>
    /// Gets the run's display title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the recorded submitter identity.
    /// </summary>
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Gets the time the run was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the time the run was queued.
    /// </summary>
    public required DateTimeOffset QueuedAt { get; init; }

    /// <summary>
    /// Gets the execution start time, or null before execution starts.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Gets the completion time, or null while unfinished.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>
    /// Gets the current run status.
    /// </summary>
    public PipelineStatus Status { get; init; }

    /// <summary>
    /// Gets the human-readable status description.
    /// </summary>
    public string StatusText { get; init; } = "";

    /// <summary>
    /// Gets the ordered log history included in this view.
    /// </summary>
    public IReadOnlyList<PipelineLogEntryView> Logs { get; init; } = [];
}

/// <summary>
/// Contains the job state needed by pipeline monitoring components.
/// </summary>
public sealed record PipelineJobView
{
    /// <summary>
    /// Gets the owning run identifier.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the job identifier within the run.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Gets the current job status.
    /// </summary>
    public JobExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the execution start time, or null before execution starts.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Gets the completion time, or null while unfinished.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }
}

/// <summary>
/// Contains the step state needed by pipeline monitoring components.
/// </summary>
public sealed record PipelineStepView
{
    /// <summary>
    /// Gets the owning run identifier.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the owning job identifier.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Gets the step identifier within its job.
    /// </summary>
    public required string StepId { get; init; }

    /// <summary>
    /// Gets the current step status.
    /// </summary>
    public StepExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the next polling time, or null when no attempt is scheduled.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; init; }

    /// <summary>
    /// Gets the polling deadline, or null for a step without a deadline.
    /// </summary>
    public DateTimeOffset? PollDeadline { get; init; }
}

/// <summary>
/// Contains the run, job, and step information used by the run-detail page.
/// </summary>
/// <param name="Run">The run metadata and logs.</param>
/// <param name="Jobs">The jobs in display order.</param>
/// <param name="Steps">The steps belonging to the run.</param>
public sealed record PipelineExecutionView(
    PipelineRunView Run,
    IReadOnlyList<PipelineJobView> Jobs,
    IReadOnlyList<PipelineStepView> Steps);