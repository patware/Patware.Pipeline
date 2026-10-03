using Microsoft.EntityFrameworkCore;

using Pipeline.Core;
using Pipeline.Persistence.EntityFrameworkCore.Entities;
using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Persists run metadata, specifications, and ordered logs through short-lived Entity Framework Core contexts.
/// </summary>
/// <param name="contextFactory">The factory supplying a short-lived database context for each operation.</param>
public sealed class EfPipelineStore(IDbContextFactory<PipelineDbContext> contextFactory) : IPipelineStore
{
    /// <summary>
    /// Atomically creates a revision-zero run, its restoration specification, and initial pending job and step states.
    /// </summary>
    /// <param name="run">The run metadata and ordered log history to persist.</param>
    /// <param name="request">The plan, serialized input and settings, and metadata to persist.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes when the initial data has been stored.</returns>
    public async Task CreateAsync(
        PipelineRun run,
        PipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (run.Revision != 0)
        {
            throw new ArgumentException(
                "A new run must have revision zero.",
                nameof(run));
        }

        if (run.Definition != request.Definition ||
            request.Plan.Definition != request.Definition)
        {
            throw new ArgumentException(
                "The run, request, and plan definitions must match.",
                nameof(request));
        }

        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = ToEntity(run);

        entity.Logs = run.Logs
            .Select((entry, sequence) =>
                ToLogEntity(run.Id, sequence, entry))
            .ToList();

        context.Runs.Add(entity);

        context.Specifications.Add(new PipelineSpecificationEntity
        {
            RunId = run.Id,
            DefinitionId = request.Definition.Id,
            DefinitionVersion = request.Definition.Version,
            InputJson = request.InputJson,
            SettingsJson = request.SettingsJson
        });

        foreach (var job in request.Plan.Jobs)
        {
            context.Jobs.Add(new PipelineJobEntity
            {
                RunId = run.Id,
                JobId = job.Id,
                Status = JobExecutionStatus.Pending,
                Revision = 0
            });

            foreach (var step in job.Steps)
            {
                context.Steps.Add(new PipelineStepEntity
                {
                    RunId = run.Id,
                    JobId = job.Id,
                    StepId = step.Id,
                    Status = StepExecutionStatus.Pending,
                    Revision = 0,
                    Attempt = 0,
                    HasOutput = false
                });
            }
        }

        // SQL Server saves these changes in one transaction.
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads run metadata and its ordered log history by identifier.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored run, or null if it does not exist.</returns>
    public async Task<PipelineRun?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.Runs
            .AsNoTracking()
            .AsSingleQuery()
            .Include(run => run.Logs)
            .SingleOrDefaultAsync(
                run => run.Id == id,
                cancellationToken);

        return entity is null ? null : ToSnapshot(entity);
    }

    /// <summary>
    /// Reads a page of runs ordered by creation time descending, then identifier.
    /// </summary>
    /// <param name="skip">The nonnegative number of runs to skip.</param>
    /// <param name="take">The positive maximum number of runs to return.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The requested page, including each run's log history.</returns>
    public async Task<IReadOnlyList<PipelineRun>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await context.Runs
            .AsNoTracking()
            .AsSingleQuery()
            .Include(run => run.Logs)
            .OrderByDescending(run => run.CreatedAt)
            .ThenBy(run => run.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Array.AsReadOnly(entities.Select(ToSnapshot).ToArray());
    }

    /// <summary>
    /// Atomically replaces run metadata and appends logs when the stored revision matches the expected revision.
    /// </summary>
    /// <param name="run">The run metadata and ordered log history to persist.</param>
    /// <param name="expectedRevision">The stored run revision that must match; the replacement run must advance it by exactly one.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if saved; false if the run is missing or a concurrent writer changed its revision.</returns>
    /// <remarks>The replacement revision must be exactly one greater than the expected revision. Existing log entries must remain unchanged and in order.</remarks>
    public async Task<bool> TryUpdateAsync(
        PipelineRun run,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        cancellationToken.ThrowIfCancellationRequested();

        if (run.Revision != checked(expectedRevision + 1))
        {
            throw new ArgumentException(
                "The replacement revision must advance by one.",
                nameof(run));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.Runs.SingleOrDefaultAsync(
            candidate => candidate.Id == run.Id,
            cancellationToken);

        if (entity is null || entity.Revision != expectedRevision)
        {
            return false;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Copies scalar properties into the tracked entity.
        // Its original Revision remains available for EF's concurrency check.
        context.Entry(entity).CurrentValues.SetValues(ToEntity(run));

        try
        {
            // First establish that this writer owns the revision change.
            // This remains uncommitted until the logs are saved too.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Disposal rolls back the uncommitted transaction.
            return false;
        }

        var existingLogs = await context.Logs
            .Where(entry => entry.RunId == run.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken);

        if (run.Logs.Count < existingLogs.Count)
        {
            throw new ArgumentException(
                "Existing log entries cannot be removed.",
                nameof(run));
        }

        for (var index = 0; index < existingLogs.Count; index++)
        {
            var existing = existingLogs[index];
            var supplied = run.Logs[index];

            if (existing.Sequence != index ||
                existing.Timestamp != supplied.Timestamp ||
                existing.Message != supplied.Message ||
                existing.Level != supplied.Level ||
                existing.JobId != supplied.JobId ||
                existing.StepId != supplied.StepId)
            {
                throw new ArgumentException(
                    "Existing log entries cannot be changed or reordered.",
                    nameof(run));
            }
        }

        for (var index = existingLogs.Count; index < run.Logs.Count; index++)
        {
            context.Logs.Add(ToLogEntity(
                run.Id,
                index,
                run.Logs[index]));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private static PipelineRunEntity ToEntity(PipelineRun run)
    {
        return new PipelineRunEntity
        {
            Id = run.Id,
            DefinitionId = run.Definition.Id,
            DefinitionDisplayName = run.Definition.DisplayName,
            DefinitionVersion = run.Definition.Version,
            Title = run.Title,
            CreatedBy = run.CreatedBy,
            Revision = run.Revision,
            CreatedAt = run.CreatedAt,
            QueuedAt = run.QueuedAt,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            Status = run.Status,
            StatusText = run.StatusText
        };
    }

    private static PipelineLogEntryEntity ToLogEntity(
        Guid runId,
        int sequence,
        PipelineLogEntry entry)
    {
        return new PipelineLogEntryEntity
        {
            RunId = runId,
            Sequence = sequence,
            Timestamp = entry.Timestamp,
            Message = entry.Message,
            Level = entry.Level,
            JobId = entry.JobId,
            StepId = entry.StepId
        };
    }

    internal static PipelineRun ToSnapshot(PipelineRunEntity entity)
    {
        return new PipelineRun
        {
            Id = entity.Id,
            Definition = new PipelineDefinition(
                entity.DefinitionId,
                entity.DefinitionDisplayName,
                entity.DefinitionVersion),
            Title = entity.Title,
            CreatedBy = entity.CreatedBy,
            Revision = entity.Revision,
            CreatedAt = entity.CreatedAt,
            QueuedAt = entity.QueuedAt,
            StartedAt = entity.StartedAt,
            FinishedAt = entity.FinishedAt,
            Status = entity.Status,
            StatusText = entity.StatusText,
            Logs = Array.AsReadOnly(
                entity.Logs
                    .OrderBy(entry => entry.Sequence)
                    .Select(entry => new PipelineLogEntry(
                        entry.Timestamp,
                        entry.Message)
                    {
                        Level = entry.Level,
                        JobId = entry.JobId,
                        StepId = entry.StepId
                    })
                    .ToArray())
        };
    }

    /// <summary>
    /// Reads the versioned definition identity and serialized input and settings for plan restoration.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored specification, or null if it does not exist.</returns>
    public async Task<PipelineExecutionSpecification?> GetSpecificationAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var context =
            await contextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await context.Specifications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                specification => specification.RunId == runId,
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new PipelineExecutionSpecification(
            entity.DefinitionId,
            entity.DefinitionVersion,
            entity.InputJson,
            entity.SettingsJson);
    }
}
