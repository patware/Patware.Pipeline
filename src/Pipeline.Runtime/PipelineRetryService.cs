namespace Pipeline.Runtime;

/// <summary>
/// Reopens failed runs while preserving successful steps, their outputs, and the existing log history.
/// </summary>
/// <remarks>This service changes persisted state but does not dispatch scheduler jobs. Use <see cref="IPipelineRuntime.RetryAsync" /> to request retry through the selected processor.</remarks>
/// <param name="executionStore">The store used to load snapshots and commit revision-checked execution transitions.</param>
/// <param name="pipelineStore">The store supplying run metadata and restoration specifications.</param>
/// <param name="definitions">The registry restoring the original versioned execution plan.</param>
/// <param name="operationGate">The shared process-local gate coordinating execution with reset, retry, and submission.</param>
/// <param name="events">Event Publication</param>
/// <param name="timeProvider">Abstraction for time</param>
public sealed class PipelineRetryService(
    IPipelineExecutionStore executionStore,
    IPipelineStore pipelineStore,
    IPipelineDefinitionRegistry definitions,
    PipelineOperationGate operationGate,
    PipelineRunEventQueue events,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Reopens a failed run for unfinished work, preserving successful steps and outputs, skipped jobs, bound arguments, and logs.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if reopened; false if missing, no longer failed, or changed concurrently.</returns>
    /// <exception cref="InvalidOperationException">A step is still running or the original specification cannot be restored.</exception>
    public Task<bool> RetryAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return operationGate.ExecuteAsync(
            () => RetryCoreAsync(runId, cancellationToken),
            cancellationToken);
    }

    private async Task<bool> RetryCoreAsync(Guid runId, CancellationToken cancellationToken)
    {
        var snapshot = await executionStore.LoadAsync(
            runId, cancellationToken);

        // Also rejects a second click after another request reopened it.
        if (snapshot is null ||
            snapshot.Run.Status != PipelineStatus.Failed)
        {
            return false;
        }

        // Do not reopen a run while an invocation might still be executing.
        if (snapshot.Steps.Any(step =>
            step.Status == StepExecutionStatus.Running))
        {
            throw new InvalidOperationException(
                "This run still has a running step.");
        }

        var specification = await pipelineStore.GetSpecificationAsync(
            runId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Run '{runId}' has no execution specification.");

        // Verify that the original definition can still be restored
        // before changing any execution state.
        _ = definitions.Restore(specification);

        var replacement = snapshot with
        {
            Run = snapshot.Run with
            {
                Status = PipelineStatus.Running,
                FinishedAt = null,
                StatusText = "Retry requested; resuming unfinished work."
            },

            Jobs = snapshot.Jobs.Select(job =>
                job.Status is JobExecutionStatus.Succeeded
                    or JobExecutionStatus.ConditionSkipped
                    ? job
                    : job with
                    {
                        Status = JobExecutionStatus.Pending,
                        ConditionResult = null,
                        StartedAt = null,
                        FinishedAt = null,
                        Error = null
                    }).ToArray(),

            Steps = snapshot.Steps.Select(step =>
                step.Status == StepExecutionStatus.Succeeded
                    ? step
                    : step with
                    {
                        Status = StepExecutionStatus.Pending,
                        StartedAt = null,
                        FinishedAt = null,
                        NextAttemptAt = null,
                        PollDeadline = null,
                        LeaseToken = null,
                        LeaseExpiresAt = null,
                        HasOutput = false,
                        OutputJson = null,
                        Error = null
                    }).ToArray()
        };

        var saved = await executionStore.TrySaveAsync(
            replacement,
            expectedRunRevision: snapshot.Run.Revision,
            message: "Manual retry requested. Successful work preserved.",
            requiredClaim: null,
            cancellationToken: cancellationToken);

        if (saved)
        {
            events.Publish(
                replacement.Run,
                PipelineRunEventKind.RetryRequested,
                checked(snapshot.Run.Revision + 1),
                timeProvider.GetUtcNow());
        }

        return saved;
    }
}