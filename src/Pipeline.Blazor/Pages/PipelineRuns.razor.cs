using Pipeline.Runtime;

using RuntimeRun = Pipeline.Runtime.PipelineRun;

namespace Pipeline.Blazor.Pages;

/// <summary>
/// Displays the latest fifty pipeline runs and their job statuses, refreshing through the existing live-page lifecycle.
/// </summary>
public partial class PipelineRuns
{
    private IReadOnlyList<RuntimeRun> _runs = [];

    private IReadOnlyDictionary<Guid, IReadOnlyList<JobExecutionState>> _jobsByRun = new Dictionary<Guid, IReadOnlyList<JobExecutionState>>();

    /// <summary>
    /// Loads recent runs and their ordered job execution states.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token used to cancel snapshot loading.
    /// </param>
    /// <returns>
    /// A task that completes after the page snapshot is updated.
    /// </returns>
    protected override async Task LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var recentRuns = await Runtime.GetRunsAsync(skip: 0, take: 50, cancellationToken: cancellationToken);

        var nextRuns = new List<RuntimeRun>(recentRuns.Count);

        var nextJobs = new Dictionary<Guid, IReadOnlyList<JobExecutionState>>(recentRuns.Count);

        foreach (var recentRun in recentRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await Runtime.GetExecutionAsync(recentRun.Id, cancellationToken);

            if (snapshot is null)
            {
                continue;
            }

            nextRuns.Add(snapshot.Run);
            nextJobs.Add(snapshot.Run.Id, snapshot.Jobs);
        }

        cancellationToken.ThrowIfCancellationRequested();

        _runs = nextRuns;
        _jobsByRun = nextJobs;

    }
}
