namespace Pipeline.Runtime;

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