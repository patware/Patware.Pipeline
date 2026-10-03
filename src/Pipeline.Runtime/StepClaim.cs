namespace Pipeline.Runtime;

/// <summary>
/// Identifies the ownership lease required to commit results and logs for a running step.
/// </summary>
/// <param name="RunId">The identifier of the run to operate on.</param>
/// <param name="JobId">The job identifier within the plan or run.</param>
/// <param name="StepId">The step identifier within its job.</param>
/// <param name="LeaseToken">The ownership token that fences results and logs from stale step invocations.</param>
/// <param name="LeaseExpiresAt">The timestamp after which the step ownership lease is no longer valid.</param>
public sealed record StepClaim(
    Guid RunId,
    string JobId,
    string StepId,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt);