using System.ComponentModel;

using global::Hangfire;

using Pipeline.Runtime;

namespace Pipeline.Hangfire;

/// <summary>
/// Executes a specific expected step attempt and requests further run coordination afterward.
/// </summary>
/// <param name="coordinator">The coordinator managing persisted execution transitions and step attempts.</param>
/// <param name="pipelineStore">The store supplying run metadata and restoration specifications.</param>
/// <param name="backgroundJobs">The Hangfire client used to enqueue or schedule work.</param>
public sealed class PipelineStepJob(
    PipelineExecutionCoordinator coordinator,
    IPipelineStore pipelineStore,
    IBackgroundJobClient backgroundJobs)
{
    /// <summary>
    /// Attempts the dispatched step and enqueues coordination if the run remains active.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="title">The user-facing title of this particular run.</param>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <param name="expectedAttempt">The positive next attempt number; a dispatch is ignored if stored progress no longer matches it.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes after execution and follow-up dispatch.</returns>
    [DisplayName(
        "{1} | Job {2} | Step {3} | Attempt {4} | Run {0}")]
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(
        Guid runId,
        string title,
        string jobId,
        string stepId,
        int expectedAttempt,
        CancellationToken cancellationToken)
    {
        var work = new PipelineStepWorkItem(
            runId,
            title,
            jobId,
            stepId,
            expectedAttempt);

        await coordinator.ExecuteStepAsync(work, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var run = await pipelineStore.GetAsync(
            runId,
            cancellationToken);

        if (run is null ||
            run.Status is not (
                PipelineStatus.Queued or
                PipelineStatus.Running))
        {
            return;
        }

        // Advance job completion, propagate failure, select another
        // step, or schedule a wake-up for the next polling attempt.
        backgroundJobs.Enqueue<PipelineRunJob>(
            job => job.ExecuteAsync(
                runId,
                run.Title,
                false,
                CancellationToken.None));
    }
}