using System.ComponentModel;

using global::Hangfire;

using Pipeline.Runtime;

namespace Pipeline.Hangfire;

/// <summary>
/// Rediscovers queued and running runs and dispatches coordination jobs for unfinished work.
/// </summary>
/// <param name="executionStore">The store used to load snapshots and commit revision-checked execution transitions.</param>
/// <param name="pipelineStore">The store supplying run metadata and restoration specifications.</param>
/// <param name="backgroundJobs">The Hangfire client used to enqueue or schedule work.</param>
public sealed class PipelineRecoveryJob(
    IPipelineExecutionStore executionStore,
    IPipelineStore pipelineStore,
    IBackgroundJobClient backgroundJobs)
{
    /// <summary>
    /// Dispatches recovery coordination for runs that are still queued or running.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes after the recovery scan and dispatch.</returns>
    [DisplayName("Pipeline recovery: discover unfinished runs")]
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var runIds = await executionStore.ListActiveRunIdsAsync(cancellationToken);

        foreach (var runId in runIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var run = await pipelineStore.GetAsync(runId, cancellationToken);

            // Reset or another invocation may have changed the run
            // since active work was discovered.
            if (run is null ||
                run.Status is not (
                    PipelineStatus.Queued or
                    PipelineStatus.Running))
            {
                continue;
            }

            backgroundJobs.Enqueue<PipelineRunJob>(
                job => job.ExecuteAsync(runId, run.Title, true, CancellationToken.None));
        }
    }
}