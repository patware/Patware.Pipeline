using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Persists run metadata, append-only logs, and the specification used to restore executable plans.
/// </summary>
public interface IPipelineStore
{
    /// <summary>
    /// Atomically creates a revision-zero run, its restoration specification, and initial pending job and step states.
    /// </summary>
    /// <param name="run">The run metadata and ordered log history to persist.</param>
    /// <param name="request">The plan, serialized input and settings, and metadata to persist.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes when the initial data has been stored.</returns>
    Task CreateAsync(PipelineRun run, PipelineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads run metadata and its ordered log history by identifier.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored run, or null if it does not exist.</returns>
    Task<PipelineRun?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of runs ordered by creation time descending, then identifier.
    /// </summary>
    /// <param name="skip">The nonnegative number of runs to skip.</param>
    /// <param name="take">The positive maximum number of runs to return.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The requested page, including each run's log history.</returns>
    Task<IReadOnlyList<PipelineRun>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces run metadata and appends logs when the stored revision matches the expected revision.
    /// </summary>
    /// <param name="run">The replacement run, including the unchanged log prefix and any new entries.</param>
    /// <param name="expectedRevision">The stored revision that must match; the replacement must advance it by exactly one.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if saved; false if the run is missing or its revision has changed.</returns>
    /// <exception cref="ArgumentException">The replacement revision is invalid or existing logs were changed, removed, or reordered.</exception>
    /// <remarks>Run state and appended logs are saved atomically; job and step states are updated through <see cref="IPipelineExecutionStore" />.</remarks>
    Task<bool> TryUpdateAsync(PipelineRun run, long expectedRevision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the versioned definition identity and serialized input and settings for plan restoration.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored specification, or null if it does not exist.</returns>
    Task<PipelineExecutionSpecification?> GetSpecificationAsync(Guid runId, CancellationToken cancellationToken = default);

}
