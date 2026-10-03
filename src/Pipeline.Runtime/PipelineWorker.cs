using Microsoft.Extensions.Hosting;

namespace Pipeline.Runtime;

/// <summary>
/// Hosts the selected runtime's execution loop as a background service.
/// </summary>
/// <param name="runtime">The runtime providing submission, query, or execution operations.</param>
public sealed class PipelineWorker(IPipelineRuntime runtime) : BackgroundService
{
    /// <summary>
    /// Runs the selected runtime's processing loop for the lifetime of the host.
    /// </summary>
    /// <param name="stoppingToken">The host shutdown token used to stop processing.</param>
    /// <returns>A task representing the processing loop.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return runtime.RunAsync(stoppingToken);
    }
}
