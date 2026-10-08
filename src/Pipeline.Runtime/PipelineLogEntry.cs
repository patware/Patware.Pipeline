namespace Pipeline.Runtime;

/// <summary>
/// Specifies the severity of a persisted pipeline log entry.
/// </summary>
public enum PipelineLogLevel
{
    /// <summary>
    /// An informational progress message.
    /// </summary>
    Information = 0,
    /// <summary>
    /// A warning message that does not itself change execution status.
    /// </summary>
    Warning = 1,
    /// <summary>
    /// An error message that does not itself fail the step.
    /// </summary>
    Error = 2
}

/// <summary>
/// Represents a timestamped message with severity and optional job and step scope.
/// </summary>
/// <param name="Timestamp">The timestamp at which the log entry was recorded.</param>
/// <param name="Message">The message to display or append to the persistent log.</param>
public sealed record PipelineLogEntry(DateTimeOffset Timestamp, string Message)
{
    /// <summary>
    /// Gets the severity of the entry, defaulting to information.
    /// </summary>
    public PipelineLogLevel Level { get; init; } = PipelineLogLevel.Information;

    /// <summary>
    /// Gets the job identifier, scoped to its pipeline plan or run. Null means the entry has no scope at this level.
    /// </summary>
    public string? JobId { get; init; }
    /// <summary>
    /// Gets the step identifier, scoped to its containing job. Null means the entry has no scope at this level.
    /// </summary>
    public string? StepId { get; init; }
}

