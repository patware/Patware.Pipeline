using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Identifies a persisted run lifecycle transition.
/// </summary>
public enum PipelineRunEventKind
{
    /// <summary>
    /// A new run was persisted and is awaiting processing.
    /// </summary>
    Queued,

    /// <summary>
    /// A queued run transitioned to running.
    /// </summary>
    Started,

    /// <summary>
    /// The run completed after all jobs succeeded or were condition-skipped.
    /// </summary>
    Completed,

    /// <summary>
    /// The run ended unsuccessfully because execution or coordination failed.
    /// </summary>
    Failed,

    /// <summary>
    /// A failed run was reopened to resume unfinished work.
    /// </summary>
    RetryRequested
}

/// <summary>
/// Describes a successfully persisted run lifecycle transition.
/// </summary>
public sealed record PipelineRunEvent(
    Guid RunId,
    long Revision,
    PipelineDefinition Definition,
    string Title,
    string CreatedBy,
    PipelineRunEventKind Kind,
    PipelineStatus Status,
    DateTimeOffset OccurredAt,
    string StatusText);