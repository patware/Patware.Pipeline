namespace Pipeline.Runtime;

/// <summary>
/// Identifies a dispatchable step and its expected next attempt so stale deliveries can be rejected.
/// </summary>
/// <param name="RunId">The identifier of the run to operate on.</param>
/// <param name="Title">The user-facing title of this particular run.</param>
/// <param name="JobId">The job identifier within the plan or run.</param>
/// <param name="StepId">The step identifier within its job.</param>
/// <param name="ExpectedAttempt">The positive next attempt number; a dispatch is ignored if stored progress no longer matches it.</param>
public sealed record PipelineStepWorkItem(
    Guid RunId,
    string Title,
    string JobId,
    string StepId,
    int ExpectedAttempt);

/// <summary>
/// Reports whether coordination changed state and whether a step was selected for separate dispatch.
/// </summary>
/// <param name="Progressed">Whether the coordinator successfully persisted an execution transition.</param>
/// <param name="Step">The selected step to dispatch, or null when no step was selected.</param>
public sealed record PipelineAdvanceResult(bool Progressed, PipelineStepWorkItem? Step);