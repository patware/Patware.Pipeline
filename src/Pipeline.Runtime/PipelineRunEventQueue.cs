using System.Threading.Channels;

namespace Pipeline.Runtime;

/// <summary>
/// Buffers process-local notifications for separate dispatch.
/// </summary>
public sealed class PipelineRunEventQueue
{
    private readonly Channel<PipelineRunEvent> channel =
        Channel.CreateUnbounded<PipelineRunEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

    internal void Publish(
        PipelineRun run,
        PipelineRunEventKind kind,
        long committedRevision,
        DateTimeOffset occurredAt)
    {
        var notification = new PipelineRunEvent(
            run.Id,
            committedRevision,
            run.Definition,
            run.Title,
            run.CreatedBy,
            kind,
            run.Status,
            occurredAt,
            run.StatusText);

        // This unbounded writer is never completed.
        channel.Writer.TryWrite(notification);
    }

    internal IAsyncEnumerable<PipelineRunEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}