using Pipeline.Runtime;

namespace Pipeline.Web.Services;

public sealed class PipelineRunNotificationHandler(ILogger<PipelineRunNotificationHandler> logger) : IPipelineRunEventHandler
{
    public Task HandleAsync(PipelineRunEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Pipeline {Title}: {Kind} " +
            "(run {RunId}, revision {Revision}).",
            notification.Title,
            notification.Kind,
            notification.RunId,
            notification.Revision);

        return Task.CompletedTask;
    }
}