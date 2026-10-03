using System.ComponentModel;

using global::Hangfire;

using Pipeline.Runtime;

namespace Pipeline.Hangfire;

/// <summary>
/// Coordinates run transitions, dispatches one eligible step, and schedules polling wake-ups.
/// </summary>
/// <param name="coordinator">The coordinator managing persisted execution transitions and step attempts.</param>
/// <param name="executionStore">The store used to load snapshots and commit revision-checked execution transitions.</param>
/// <param name="backgroundJobs">The Hangfire client used to enqueue or schedule work.</param>
/// <param name="timeProvider">The clock used for timestamps, polling deadlines, or lease validity.</param>
public sealed class PipelineRunJob(
    PipelineExecutionCoordinator coordinator,
    IPipelineExecutionStore executionStore,
    IBackgroundJobClient backgroundJobs,
    TimeProvider timeProvider)
{
    private const int TransitionLimit = 100;

    /// <summary>
    /// Advances coordination and dispatches eligible steps or schedules a polling wake-up.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="title">The user-facing title of this particular run.</param>
    /// <param name="recovery">Whether this is a recovery probe; idle probes avoid scheduling duplicate delayed wake-ups.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes after this coordination pass.</returns>
    [DisplayName("Coordinate: {1} | Run {0}")]
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(
        Guid runId,
        string title,
        bool recovery,
        CancellationToken cancellationToken)
    {
        var transitions = 0;

        while (transitions < TransitionLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await coordinator.PrepareNextStepAsync(
                runId,
                cancellationToken);

            if (result.Step is { } work)
            {
                cancellationToken.ThrowIfCancellationRequested();

                backgroundJobs.Enqueue<PipelineStepJob>(
                    job => job.ExecuteAsync(
                        work.RunId,
                        work.Title,
                        work.JobId,
                        work.StepId,
                        work.ExpectedAttempt,
                        CancellationToken.None));

                // The step job will request further coordination.
                // Recovery covers interruption before that happens.
                return;
            }

            if (!result.Progressed)
            {
                break;
            }

            transitions++;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = await executionStore.LoadAsync(
            runId,
            cancellationToken);

        if (snapshot is null || IsTerminal(snapshot.Run.Status))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        if (snapshot.Steps.Any(step =>
            step.Status == StepExecutionStatus.Running &&
            step.LeaseExpiresAt > now))
        {
            // An existing owner will request further coordination.
            // Recovery handles abandoned leases.
            return;
        }

        if (recovery && transitions == 0)
        {
            // Avoid creating another delayed wake-up for every
            // recovery probe when no work is currently ready.
            return;
        }

        var nextAttempt = now.AddSeconds(1);

        if (transitions < TransitionLimit)
        {
            var waitingUntil = snapshot.Steps
                .Where(step =>
                    step.Status == StepExecutionStatus.Waiting)
                .Select(step =>
                {
                    var due = step.NextAttemptAt ?? now;

                    if (step.PollDeadline is { } deadline &&
                        deadline < due)
                    {
                        due = deadline;
                    }

                    return (DateTimeOffset?)due;
                })
                .Min();

            if (waitingUntil is { } due && due > now)
            {
                nextAttempt = due;
            }
        }

        backgroundJobs.Schedule<PipelineRunJob>(
            job => job.ExecuteAsync(
                runId,
                snapshot.Run.Title,
                false,
                CancellationToken.None),
            nextAttempt);
    }

    // Compatibility with the original persisted job signature.
    /// <summary>
    /// Advances coordination and dispatches eligible steps or schedules a polling wake-up.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="recovery">Whether this is a recovery probe; idle probes avoid scheduling duplicate delayed wake-ups.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes after this coordination pass.</returns>
    /// <remarks>Compatibility entry point for older persisted Hangfire jobs. Resolves the title from the stored run; missing runs are ignored.</remarks>
    [DisplayName("Coordinate pipeline run {0}")]
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(
        Guid runId,
        bool recovery,
        CancellationToken cancellationToken)
    {
        var snapshot = await executionStore.LoadAsync(
            runId,
            cancellationToken);

        if (snapshot is null)
        {
            return;
        }

        await ExecuteAsync(
            runId,
            snapshot.Run.Title,
            recovery,
            cancellationToken);
    }

    // Compatibility with the six-argument signature.
    // Its job/step values never constrained the old implementation,
    // so this remains a coordination entry point.
    /// <summary>
    /// Advances coordination and dispatches eligible steps or schedules a polling wake-up.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="title">The user-facing title of this particular run.</param>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <param name="recovery">Whether this is a recovery probe; idle probes avoid scheduling duplicate delayed wake-ups.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes after this coordination pass.</returns>
    /// <remarks>Compatibility entry point for persisted Hangfire jobs. The job and step arguments are ignored; this coordinates the whole run.</remarks>
    [DisplayName("Coordinate: {1} | Run {0}")]
    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync(
        Guid runId,
        string title,
        string jobId,
        string stepId,
        bool recovery,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            runId,
            title,
            recovery,
            cancellationToken);
    }

    private static bool IsTerminal(PipelineStatus status)
    {
        return status is
            PipelineStatus.Completed or
            PipelineStatus.Failed or
            PipelineStatus.Cancelled;
    }
}