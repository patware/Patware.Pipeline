using System.Data;

using Microsoft.EntityFrameworkCore;

using Pipeline.Persistence.EntityFrameworkCore.Entities;
using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Uses database transactions and concurrency revisions to load and atomically update execution state.
/// </summary>
/// <param name="contextFactory">The factory supplying a short-lived database context for each operation.</param>
/// <param name="timeProvider">The clock used for timestamps, polling deadlines, or lease validity.</param>
public sealed class EfPipelineExecutionStore(IDbContextFactory<PipelineDbContext> contextFactory, TimeProvider timeProvider) : IPipelineExecutionStore
{
    /// <summary>
    /// Finds queued and running runs for processing or recovery.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The active run identifiers in queue order.</returns>
    public async Task<IReadOnlyList<Guid>> ListActiveRunIdsAsync(CancellationToken cancellationToken)
    {
        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Runs
            .AsNoTracking()
            .Where(run =>
                run.Status == PipelineStatus.Queued ||
                run.Status == PipelineStatus.Running)
            .Where(run => context.Specifications.Any(
                specification => specification.RunId == run.Id))
            .OrderBy(run => run.QueuedAt)
            .Select(run => run.Id)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a consistent view of the run and all its job and step execution states.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The execution snapshot, or null if the run does not exist.</returns>
    public async Task<PipelineExecutionSnapshot?> LoadAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        // Keep this transaction short. No external calls occur here.
        await using var transaction =
            await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var run = await context.Runs
            .AsNoTracking()
            .AsSingleQuery()
            .Include(candidate => candidate.Logs)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == runId,
                cancellationToken);

        if (run is null)
        {
            return null;
        }

        var jobs = await context.Jobs
            .AsNoTracking()
            .Where(job => job.RunId == runId)
            .ToArrayAsync(cancellationToken);

        var steps = await context.Steps
            .AsNoTracking()
            .Where(step => step.RunId == runId)
            .ToArrayAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new PipelineExecutionSnapshot(
            EfPipelineStore.ToSnapshot(run),
            jobs.Select(ToState).ToArray(),
            steps.Select(ToState).ToArray());
    }

    /// <summary>
    /// Atomically commits execution state and appends one scoped log entry, checking the run revision and any required live step claim.
    /// </summary>
    /// <param name="replacement">The proposed state for the existing execution graph; stored logs are preserved independently.</param>
    /// <param name="expectedRunRevision">The stored run revision that must match; the store advances all state revisions on commit.</param>
    /// <param name="message">The message to display or append to the persistent log.</param>
    /// <param name="requiredClaim">The live ownership claim to verify, or null for an unclaimed coordination transition.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <param name="level">The severity of the entry, defaulting to information.</param>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <returns>True if committed; false if the run is missing, its revision changed, or the required claim is no longer valid.</returns>
    /// <remarks>The replacement must retain the stored job and step identities. A step scope requires a job scope; when a claim is supplied, both must match it. The store advances revisions and preserves existing logs.</remarks>
    public async Task<bool> TrySaveAsync(
        PipelineExecutionSnapshot replacement,
        long expectedRunRevision,
        string message,
        StepClaim? requiredClaim,
        CancellationToken cancellationToken,
        PipelineLogLevel level = PipelineLogLevel.Information,
        string? jobId = null,
        string? stepId = null)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (jobId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        }

        if (stepId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

            if (jobId is null)
            {
                throw new ArgumentException(
                    "A step log must identify its job.",
                    nameof(jobId));
            }
        }

        if (jobId is not null && !replacement.Jobs.Any(job =>
            job.RunId == replacement.Run.Id &&
            job.JobId == jobId))
        {
            throw new ArgumentException(
                "The log job does not belong to this run.",
                nameof(jobId));
        }

        if (stepId is not null &&
            !replacement.Steps.Any(step =>
                step.RunId == replacement.Run.Id &&
                step.JobId == jobId &&
                step.StepId == stepId))
        {
            throw new ArgumentException(
                "The log step does not belong to this job.",
                nameof(stepId));
        }

        if (requiredClaim is not null &&
            (jobId != requiredClaim.JobId ||
             stepId != requiredClaim.StepId))
        {
            throw new ArgumentException(
                "A claimed step must write logs under its own identity.");
        }

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                cancellationToken);

        var run = await context.Runs.SingleOrDefaultAsync(
            candidate => candidate.Id == replacement.Run.Id,
            cancellationToken);

        if (run is null || run.Revision != expectedRunRevision)
        {
            return false;
        }

        // Acquire the run's write ownership through its concurrency token.
        // This change remains uncommitted until everything below is saved.
        run.Revision = checked(run.Revision + 1);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        var jobs = await context.Jobs
            .Where(job => job.RunId == run.Id)
            .ToArrayAsync(cancellationToken);

        var steps = await context.Steps
            .Where(step => step.RunId == run.Id)
            .ToArrayAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (requiredClaim is not null)
        {
            var owned = steps.SingleOrDefault(step =>
                step.JobId == requiredClaim.JobId &&
                step.StepId == requiredClaim.StepId);

            if (requiredClaim.RunId != run.Id ||
                owned is null ||
                owned.Status != StepExecutionStatus.Running ||
                owned.LeaseToken != requiredClaim.LeaseToken ||
                owned.LeaseExpiresAt is null ||
                owned.LeaseExpiresAt <= now)
            {
                return false;
            }
        }

        var replacementJobs = replacement.Jobs.ToDictionary(job => job.JobId);

        var replacementSteps = replacement.Steps.ToDictionary(step => (step.JobId, step.StepId));

        if (replacement.Jobs.Any(job => job.RunId != run.Id) ||
            replacement.Steps.Any(step => step.RunId != run.Id) ||
            jobs.Length != replacementJobs.Count ||
            steps.Length != replacementSteps.Count)
        {
            throw new InvalidOperationException("The replacement does not match the stored execution graph.");
        }

        foreach (var job in jobs)
        {
            if (!replacementJobs.TryGetValue(job.JobId, out var state))
            {
                throw new InvalidOperationException($"Job '{job.JobId}' is missing from the replacement.");
            }

            var nextRevision = checked(job.Revision + 1);

            context.Entry(job).CurrentValues.SetValues(state);

            job.Revision = nextRevision;
        }

        foreach (var step in steps)
        {
            if (!replacementSteps.TryGetValue((step.JobId, step.StepId), out var state))
            {
                throw new InvalidOperationException($"Step '{step.JobId}/{step.StepId}' is missing.");
            }

            var nextRevision = checked(step.Revision + 1);

            context.Entry(step).CurrentValues.SetValues(state);
            step.Revision = nextRevision;
        }

        run.Status = replacement.Run.Status;
        run.StatusText = replacement.Run.StatusText;
        run.StartedAt = replacement.Run.StartedAt;
        run.FinishedAt = replacement.Run.FinishedAt;

        var lastSequence = await context.Logs
            .Where(log => log.RunId == run.Id)
            .MaxAsync(
                log => (int?)log.Sequence,
                cancellationToken);

        context.Logs.Add(new PipelineLogEntryEntity
        {
            RunId = run.Id,
            Sequence = checked((lastSequence ?? -1) + 1),
            Timestamp = now,
            Message = message,
            Level = level,
            JobId = jobId,
            StepId = stepId
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private static JobExecutionState ToState(PipelineJobEntity job)
    {
        return new JobExecutionState
        {
            RunId = job.RunId,
            JobId = job.JobId,
            Status = job.Status,
            Revision = job.Revision,
            ConditionResult = job.ConditionResult,
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt,
            Error = job.Error
        };
    }

    private static StepExecutionState ToState(PipelineStepEntity step)
    {
        return new StepExecutionState
        {
            RunId = step.RunId,
            JobId = step.JobId,
            StepId = step.StepId,
            Status = step.Status,
            Revision = step.Revision,
            Attempt = step.Attempt,
            ArgumentsJson = step.ArgumentsJson,
            HasOutput = step.HasOutput,
            OutputJson = step.OutputJson,
            StartedAt = step.StartedAt,
            FinishedAt = step.FinishedAt,
            NextAttemptAt = step.NextAttemptAt,
            PollDeadline = step.PollDeadline,
            LeaseToken = step.LeaseToken,
            LeaseExpiresAt = step.LeaseExpiresAt,
            Error = step.Error
        };
    }
}