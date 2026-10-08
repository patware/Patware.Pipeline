using Pipeline.Contracts;

namespace Pipeline.Blazor.Pages;

/// <summary>
/// Displays recent pipeline runs and their job statuses.
/// </summary>
public partial class PipelineRuns
{
    private IReadOnlyList<PipelineRunSummaryView> _runs = [];

    /// <summary>
    /// Loads the run overview through one monitoring operation.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token used to cancel snapshot loading.
    /// </param>
    /// <returns>
    /// A task that completes after the page snapshot is updated.
    /// </returns>
    protected override async Task LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var summaries = await Monitor.GetRunsAsync(
            skip: 0,
            take: 50,
            cancellationToken: cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        _runs = summaries;
    }
}