namespace Pipeline.Runtime;

/// <summary>
/// Handles process-local run lifecycle notifications.
/// </summary>
public interface IPipelineRunEventHandler
{
    /// <summary>
    /// Reacts to a persisted transition without changing its outcome.
    /// </summary>
    Task HandleAsync(PipelineRunEvent notification, CancellationToken cancellationToken);
}