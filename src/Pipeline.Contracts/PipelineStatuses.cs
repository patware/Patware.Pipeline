namespace Pipeline.Contracts;

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
/// Describes a job's progress or terminal outcome within a run.
/// </summary>
public enum JobExecutionStatus
{
    /// <summary>
    /// Awaiting prerequisites or selection for execution.
    /// </summary>
    Pending,
    /// <summary>
    /// Execution has started and has not reached a terminal outcome.
    /// </summary>
    Running,
    /// <summary>
    /// Completed successfully.
    /// </summary>
    Succeeded,
    /// <summary>
    /// Skipped because the job's condition evaluated to false.
    /// </summary>
    ConditionSkipped,
    /// <summary>
    /// Ended unsuccessfully because execution or coordination failed.
    /// </summary>
    Failed,
    /// <summary>
    /// Ended because a polling deadline expired before the check succeeded.
    /// </summary>
    TimedOut,
    /// <summary>
    /// Execution was cancelled.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Cannot proceed because failure elsewhere in the run prevents execution.
    /// </summary>
    Blocked
}

/// <summary>
/// Describes a step's progress, polling wait, or terminal outcome.
/// </summary>
public enum StepExecutionStatus
{
    /// <summary>
    /// Awaiting prerequisites or selection for execution.
    /// </summary>
    Pending,
    /// <summary>
    /// Execution has started and has not reached a terminal outcome.
    /// </summary>
    Running,
    /// <summary>
    /// An unsuccessful polling check is waiting for its next scheduled attempt.
    /// </summary>
    Waiting,
    /// <summary>
    /// Completed successfully.
    /// </summary>
    Succeeded,
    /// <summary>
    /// Ended unsuccessfully because execution or coordination failed.
    /// </summary>
    Failed,
    /// <summary>
    /// Ended because a polling deadline expired before the check succeeded.
    /// </summary>
    TimedOut,
    /// <summary>
    /// Execution was cancelled.
    /// </summary>
    Cancelled
}

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