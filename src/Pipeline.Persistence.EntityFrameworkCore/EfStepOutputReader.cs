using Microsoft.EntityFrameworkCore;

using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Reads a producer's success flag and serialized output without tracking database entities.
/// </summary>
/// <param name="contextFactory">The factory supplying a short-lived database context for each operation.</param>
public sealed class EfStepOutputReader(IDbContextFactory<PipelineDbContext> contextFactory) : IStepOutputReader
{
    /// <summary>
    /// Reads a step's producer success and output availability without invoking the step.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored output metadata, or null if the run or step does not exist.</returns>
    public async Task<StoredStepOutput?> ReadAsync(
        Guid runId,
        string jobId,
        string stepId,
        CancellationToken cancellationToken)
    {
        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var step = await context.Steps
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.RunId == runId &&
                    candidate.JobId == jobId &&
                    candidate.StepId == stepId,
                cancellationToken);

        if (step is null)
        {
            return null;
        }

        return new StoredStepOutput(
            ProducerSucceeded:
                step.Status == StepExecutionStatus.Succeeded,
            HasOutput: step.HasOutput,
            OutputJson: step.OutputJson);
    }
}