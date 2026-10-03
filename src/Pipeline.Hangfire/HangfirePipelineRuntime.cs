using global::Hangfire;

using Microsoft.Extensions.Logging;

using Pipeline.Core;
using Pipeline.Runtime;

namespace Pipeline.Hangfire;

/// <summary>
/// Uses shared run persistence and dispatches execution through Hangfire, with recovery for failed dispatches.
/// </summary>
/// <param name="runtime">The runtime providing submission, query, or execution operations.</param>
/// <param name="backgroundJobs">The Hangfire client used to enqueue or schedule work.</param>
/// <param name="logger">The logger receiving diagnostic or invocation messages.</param>
public sealed class HangfirePipelineRuntime(PipelineRuntime runtime, IBackgroundJobClient backgroundJobs, ILogger<HangfirePipelineRuntime> logger) : IPipelineRuntime
{
    /// <summary>
    /// Persists a new queued run and its initial execution graph for the selected processor.
    /// </summary>
    /// <param name="request">The plan, serialized input and settings, and metadata to persist.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The persisted run with its new identifier and initial log entry.</returns>
    /// <remarks>The run is persisted before dispatch. A dispatch failure is logged and left for recovery while the selected store retains the run.</remarks>
    public async Task<PipelineRun> EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        var run = await runtime.EnqueueAsync(request, cancellationToken);

        try
        {
            backgroundJobs.Enqueue<PipelineRunJob>(
                job => job.ExecuteAsync(
                    run.Id,
                    run.Title,
                    false,
                    CancellationToken.None));
        }
        catch (Exception exception)
        {
            // The run is already saved in the selected store.
            // Recovery can rediscover it while that store retains it.
            logger.LogError(
                exception,
                "Pipeline run {RunId} was saved, but Hangfire dispatch " +
                "failed. Recovery will retry dispatch.",
                run.Id);
        }

        return run;
    }

    /// <summary>
    /// Reads one run's metadata and ordered logs.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The run, or null if it no longer exists.</returns>
    public Task<PipelineRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return runtime.GetRunAsync(id, cancellationToken);
    }

    /// <summary>
    /// Reads a page of runs ordered by creation time descending, then identifier.
    /// </summary>
    /// <param name="skip">The nonnegative number of runs to skip.</param>
    /// <param name="take">The positive maximum number of runs to return.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The requested page of runs and their logs.</returns>
    public Task<IReadOnlyList<PipelineRun>> GetRunsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return runtime.GetRunsAsync(skip, take, cancellationToken);
    }

    /// <summary>
    /// Reads execution state and orders jobs according to the restored plan.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The snapshot, or null if the run no longer exists.</returns>
    public Task<PipelineExecutionSnapshot?> GetExecutionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return runtime.GetExecutionAsync(id, cancellationToken);
    }

    /// <summary>
    /// Rejects direct loop execution because Hangfire owns processing.
    /// </summary>
    /// <param name="stoppingToken">The host shutdown token used to stop processing.</param>
    /// <exception cref="InvalidOperationException">Always thrown; use Hangfire workers instead of PipelineWorker.</exception>
    public Task RunAsync(CancellationToken stoppingToken)
    {
        throw new InvalidOperationException(
            "Hangfire owns execution. Do not register PipelineWorker " +
            "when using HangfirePipelineRuntime.");
    }

    /// <summary>
    /// Reopens a failed run for unfinished work, preserving successful steps and outputs, skipped jobs, bound arguments, and logs.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if reopened; false if missing, no longer failed, or changed concurrently.</returns>
    /// <exception cref="InvalidOperationException">A step is still running or the original specification cannot be restored.</exception>
    public async Task<bool> RetryAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await runtime.RetryAsync(runId, cancellationToken))
        {
            return false;
        }

        try
        {
            var run = await runtime.GetRunAsync(runId, cancellationToken);

            if (run is not null)
            {
                backgroundJobs.Enqueue<PipelineRunJob>(
                    job => job.ExecuteAsync(
                        run.Id,
                        run.Title,
                        false,
                        CancellationToken.None));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Pipeline run {RunId} was reopened, but Hangfire dispatch " +
                "failed. Recovery will retry dispatch.",
                runId);
        }

        return true;
    }

}