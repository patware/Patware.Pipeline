namespace Pipeline.Runtime;

/// <summary>
/// Removes all pipeline runs and their associated persisted data.
/// </summary>
/// <remarks>Reset removes pipeline data, including logs and outputs. It does not clear an external scheduler's queue; stale deliveries must tolerate missing runs.</remarks>
public interface IPipelineResetService
{
    /// <summary>
    /// Deletes all runs and their specifications, job and step state, outputs, and logs.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The number of runs removed.</returns>
    Task<int> ResetAsync(CancellationToken cancellationToken = default);
}