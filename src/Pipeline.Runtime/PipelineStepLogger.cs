using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Persists logs for the active invocation only while its step lease remains valid.
/// </summary>
/// <param name="executionStore">The store used to load snapshots and commit revision-checked execution transitions.</param>
/// <param name="timeProvider">The clock used for timestamps, polling deadlines, or lease validity.</param>
public sealed class PipelineStepLogger(
    IPipelineExecutionStore executionStore,
    TimeProvider timeProvider) : IPipelineStepLogger
{
    private StepClaim? claim;

    internal void Initialize(StepClaim stepClaim)
    {
        ArgumentNullException.ThrowIfNull(stepClaim);

        if (claim is not null)
        {
            throw new InvalidOperationException(
                "This logger already belongs to a step invocation.");
        }

        claim = stepClaim;
    }

    /// <summary>
    /// Appends an informational message to the current step's persistent log scope.
    /// </summary>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes once the entry has been persisted.</returns>
    /// <exception cref="InvalidOperationException">There is no active invocation, its lease was lost, or the log could not be persisted after concurrent updates.</exception>
    public Task InformationAsync(
        string message,
        CancellationToken cancellationToken = default) =>
        WriteAsync(message, PipelineLogLevel.Information, cancellationToken);

    /// <summary>
    /// Appends a warning message to the current step's persistent log scope.
    /// </summary>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes once the entry has been persisted.</returns>
    /// <exception cref="InvalidOperationException">There is no active invocation, its lease was lost, or the log could not be persisted after concurrent updates.</exception>
    public Task WarningAsync(
        string message,
        CancellationToken cancellationToken = default) =>
        WriteAsync(message, PipelineLogLevel.Warning, cancellationToken);

    /// <summary>
    /// Appends an error message to the current step's persistent log scope; logging alone does not fail the step.
    /// </summary>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes once the entry has been persisted.</returns>
    /// <exception cref="InvalidOperationException">There is no active invocation, its lease was lost, or the log could not be persisted after concurrent updates.</exception>
    public Task ErrorAsync(
        string message,
        CancellationToken cancellationToken = default) =>
        WriteAsync(message, PipelineLogLevel.Error, cancellationToken);

    private async Task WriteAsync(
        string message,
        PipelineLogLevel level,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var owner = claim
            ?? throw new InvalidOperationException(
                "Pipeline logging requires an active step invocation.");

        // A renewal or another writer can advance the run revision.
        // Reload before each retry instead of reusing stale state.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await executionStore.LoadAsync(
                owner.RunId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Pipeline run '{owner.RunId}' no longer exists.");

            var step = snapshot.Steps.SingleOrDefault(candidate =>
                candidate.JobId == owner.JobId &&
                candidate.StepId == owner.StepId);

            if (step is null ||
                step.Status != StepExecutionStatus.Running ||
                step.LeaseToken != owner.LeaseToken ||
                step.LeaseExpiresAt is null ||
                step.LeaseExpiresAt <= timeProvider.GetUtcNow())
            {
                throw new InvalidOperationException(
                    "Cannot write logs after losing step ownership.");
            }

            var saved = await executionStore.TrySaveAsync(
                snapshot,
                snapshot.Run.Revision,
                message,
                requiredClaim: owner,
                cancellationToken: cancellationToken,
                level: level,
                jobId: owner.JobId,
                stepId: owner.StepId);

            if (saved)
                return;

            if (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), cancellationToken);
            }
        }

        throw new InvalidOperationException("Could not persist the log message after concurrent updates.");
    }
}