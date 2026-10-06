using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Initializes pipeline storage before hosted services begin processing.
/// </summary>
internal sealed class PipelineDatabaseInitializer(
    IDbContextFactory<PipelineDbContext> contextFactory,
    ILogger<PipelineDatabaseInitializer> logger)
    : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Preparing pipeline database.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Pipeline database is ready.");
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}