namespace Pipeline.Runtime;

/// <summary>
/// Loads execution snapshots and atomically commits revision-checked state transitions with a log entry.
/// </summary>
public interface IPipelineExecutionStore
{
    /// <summary>
    /// Finds queued and running runs for processing or recovery.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The active run identifiers in queue order.</returns>
    Task<IReadOnlyList<Guid>> ListActiveRunIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads a consistent view of the run and all its job and step execution states.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The execution snapshot, or null if the run does not exist.</returns>
    Task<PipelineExecutionSnapshot?> LoadAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically commits execution state and appends one scoped log entry, checking the run revision and any required live step claim.
    /// </summary>
    /// <param name="replacement">The proposed state for the existing execution graph; stored logs are preserved independently.</param>
    /// <param name="expectedRunRevision">The stored run revision that must match; the store advances all state revisions on commit.</param>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="requiredClaim">The live ownership claim to verify, or null for an unclaimed coordination transition.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <param name="level">The severity of the entry, defaulting to information.</param>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <returns>True if committed; false if the run is missing, its revision changed, or the required claim is no longer valid.</returns>
    /// <remarks>The replacement must retain the stored job and step identities. A step scope requires a job scope; when a claim is supplied, both must match it. The store advances revisions and preserves existing logs.</remarks>
    Task<bool> TrySaveAsync(
        PipelineExecutionSnapshot replacement,
        long expectedRunRevision,
        string message,
        StepClaim? requiredClaim,
        CancellationToken cancellationToken,
        PipelineLogLevel level = PipelineLogLevel.Information,
        string? jobId = null,
        string? stepId = null);
}