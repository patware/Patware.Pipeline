using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Pipeline.Core.Pipelines;

namespace Pipeline.Runtime.Hosting;

/// <summary>
/// Submits the built-in log-formatting demonstration during host startup.
/// </summary>
/// <remarks>
/// Registration is controlled by
/// <see cref="PipelineRegistrationOptions.RunLogFormattingDemoOnStartup"/>.
/// A scope accommodates scoped dependencies in pipeline definitions.
/// Submission uses the selected runtime so processor-specific dispatch,
/// including Hangfire dispatch, remains effective.
/// Each enabled host startup submits one new demonstration run.
/// </remarks>
internal sealed class LogFormattingDemoStartupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogFormattingDemoStartupService> _logger;

    /// <summary>
    /// Initializes the demonstration submitter.
    /// </summary>
    /// <param name="scopeFactory">
    /// The factory used to resolve scoped submission services.
    /// </param>
    /// <param name="logger">
    /// The logger recording the submitted run identifier.
    /// </param>
    public LogFormattingDemoStartupService(IServiceScopeFactory scopeFactory, ILogger<LogFormattingDemoStartupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var pipeline = scope.ServiceProvider
            .GetRequiredService<LogFormattingPipeline>();

        var runtime = scope.ServiceProvider
            .GetRequiredService<IPipelineRuntime>();

        var run = await runtime.EnqueueAsync(pipeline.Build("Log formatting demonstration"), cancellationToken);

        _logger.LogInformation("Submitted log-formatting demonstration run {RunId}.", run.Id);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}