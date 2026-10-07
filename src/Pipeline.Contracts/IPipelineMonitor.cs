namespace Pipeline.Contracts;

/// <summary>
/// Provides transport-independent run queries and retry requests.
/// </summary>
public interface IPipelineMonitor
{
    /// <summary>
    /// Gets a page of recent run summaries, including ordered job statuses.
    /// </summary>
    /// <param name="skip">
    /// The nonnegative number of runs to skip.
    /// </param>
    /// <param name="take">
    /// The positive maximum number of runs to return.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The requested page of summaries, excluding logs and step details.
    /// </returns>
    Task<IReadOnlyList<PipelineRunSummaryView>> GetRunsAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the information needed to display one run's execution.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The execution view, or null when the run does not exist.
    /// </returns>
    Task<PipelineExecutionView?> GetExecutionAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests that a failed run resume its unfinished work.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True when the run was reopened; false when it was missing,
    /// ineligible, or changed concurrently.
    /// </returns>
    Task<bool> RetryAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}