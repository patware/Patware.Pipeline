using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Submits, queries, and retries runs, and exposes the execution loop for processors that use it.
/// </summary>
public interface IPipelineRuntime
{
    /// <summary>
    /// Persists a new queued run and its initial execution graph for the selected processor.
    /// </summary>
    /// <param name="request">The plan, serialized input and settings, and metadata to persist.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The persisted run with its new identifier and initial log entry.</returns>
    Task<PipelineRun> EnqueueAsync(
        PipelineRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one run's metadata and ordered logs.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The run, or null if it no longer exists.</returns>
    Task<PipelineRun?> GetRunAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of runs ordered by creation time descending, then identifier.
    /// </summary>
    /// <param name="skip">The nonnegative number of runs to skip.</param>
    /// <param name="take">The positive maximum number of runs to return.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The requested page of runs and their logs.</returns>
    Task<IReadOnlyList<PipelineRun>> GetRunsAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the processor's execution loop until host shutdown, when the selected processor exposes a loop.
    /// </summary>
    /// <param name="stoppingToken">The host shutdown token used to stop processing.</param>
    /// <returns>A task representing the lifetime of the processing loop.</returns>
    /// <exception cref="InvalidOperationException">The loop is already started or the selected processor delegates execution to an external scheduler.</exception>
    Task RunAsync(CancellationToken stoppingToken);

    /// <summary>
    /// Reads execution state and orders jobs according to the restored plan.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The snapshot, or null if the run no longer exists.</returns>
    Task<PipelineExecutionSnapshot?> GetExecutionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a failed run for unfinished work, preserving successful steps and outputs, skipped jobs, bound arguments, and logs.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if reopened; false if missing, no longer failed, or changed concurrently.</returns>
    /// <exception cref="InvalidOperationException">A step is still running or the original specification cannot be restored.</exception>
    Task<bool> RetryAsync(Guid runId, CancellationToken cancellationToken = default);
}