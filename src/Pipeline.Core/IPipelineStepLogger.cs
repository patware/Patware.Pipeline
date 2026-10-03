namespace Pipeline.Core;

/// <summary>
/// Writes persistent, severity-aware log entries scoped to the active step invocation.
/// </summary>
public interface IPipelineStepLogger
{
    /// <summary>
    /// Appends an informational message to the current step's persistent log scope.
    /// </summary>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes once the entry has been persisted.</returns>
    /// <exception cref="InvalidOperationException">There is no active invocation, its lease was lost, or the log could not be persisted after concurrent updates.</exception>
    Task InformationAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a warning message to the current step's persistent log scope.
    /// </summary>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes once the entry has been persisted.</returns>
    /// <exception cref="InvalidOperationException">There is no active invocation, its lease was lost, or the log could not be persisted after concurrent updates.</exception>
    Task WarningAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an error message to the current step's persistent log scope; logging alone does not fail the step.
    /// </summary>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes once the entry has been persisted.</returns>
    /// <exception cref="InvalidOperationException">There is no active invocation, its lease was lost, or the log could not be persisted after concurrent updates.</exception>
    Task ErrorAsync(string message, CancellationToken cancellationToken = default);
}