using Microsoft.EntityFrameworkCore;

using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Deletes all runs using database cascades while coordinating with local pipeline operations.
/// </summary>
/// <param name="contextFactory">The factory supplying a short-lived database context for each operation.</param>
/// <param name="operationGate">The shared process-local gate coordinating execution with reset, retry, and submission.</param>
public sealed class EfPipelineResetService(
    IDbContextFactory<PipelineDbContext> contextFactory,
    PipelineOperationGate operationGate) : IPipelineResetService
{
    /// <summary>
    /// Deletes all runs and their specifications, job and step state, outputs, and logs.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The number of runs removed.</returns>
    public Task<int> ResetAsync(CancellationToken cancellationToken = default)
    {
        return operationGate.ExecuteAsync(
            () => DeleteRunsAsync(cancellationToken),
            cancellationToken);
    }

    private async Task<int> DeleteRunsAsync(CancellationToken cancellationToken)
    {
        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        // Database cascades delete logs, specifications, jobs, and steps.
        return await context.Runs.ExecuteDeleteAsync(cancellationToken);
    }
}