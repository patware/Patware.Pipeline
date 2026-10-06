using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pipeline.Runtime;

/// <summary>
/// Dispatches lifecycle notifications independently of pipeline execution.
/// </summary>
public sealed class PipelineRunEventDispatcher(
    PipelineRunEventQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PipelineRunEventDispatcher> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (
                var notification in queue.ReadAllAsync(stoppingToken))
            {
                await DispatchAsync(notification, stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Notifications remaining in memory are not durable.
        }
    }

    private async Task DispatchAsync(PipelineRunEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var handlers = scope.ServiceProvider
                .GetServices<IPipelineRunEventHandler>()
                .ToArray();

            foreach (var handler in handlers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await handler.HandleAsync(notification, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Handler {Handler} failed for pipeline run " +
                        "{RunId}, revision {Revision}, event {Kind}.",
                        handler.GetType().FullName,
                        notification.RunId,
                        notification.Revision,
                        notification.Kind);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not dispatch pipeline event for run " +
                "{RunId}, revision {Revision}.",
                notification.RunId,
                notification.Revision);
        }
    }
}