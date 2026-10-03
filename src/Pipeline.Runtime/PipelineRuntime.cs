using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Implements run submission and queries and the built-in polling processor using registered persistence services.
/// </summary>
/// <param name="store">The store used to persist and query runs and their specifications.</param>
/// <param name="executionStore">The store used to load snapshots and commit revision-checked execution transitions.</param>
/// <param name="scopeFactory">The factory supplying scoped execution and definition services.</param>
/// <param name="timeProvider">The clock used for timestamps, polling deadlines, or lease validity.</param>
/// <param name="logger">The logger receiving diagnostic or invocation messages.</param>
/// <param name="operationGate">The shared process-local gate coordinating execution with reset, retry, and submission.</param>
public sealed class PipelineRuntime(
    IPipelineStore store,
    IPipelineExecutionStore executionStore,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PipelineRuntime> logger,
    PipelineOperationGate operationGate) : IPipelineRuntime
{
    private int _runnerStarted;

    /// <summary>
    /// Persists a new queued run and its initial execution graph for the selected processor.
    /// </summary>
    /// <param name="request">The plan, serialized input and settings, and metadata to persist.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The persisted run with its new identifier and initial log entry.</returns>
    public Task<PipelineRun> EnqueueAsync(
    PipelineRequest request,
    CancellationToken cancellationToken = default)
    {
        return operationGate.ExecuteAsync(() => EnqueueCoreAsync(request, cancellationToken), cancellationToken);
    }
    private async Task<PipelineRun> EnqueueCoreAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();

        var run = new PipelineRun
        {
            Id = Guid.NewGuid(),
            Definition = request.Definition,
            Title = request.Title,
            CreatedBy = request.CreatedBy,
            Revision = 0,
            CreatedAt = now,
            QueuedAt = now,
            Status = PipelineStatus.Queued,
            StatusText = "Waiting for execution.",
            Logs = Array.AsReadOnly(
            [
                new PipelineLogEntry(now, "Run queued.")
            ])
        };

        await store.CreateAsync(
            run,
            request,
            cancellationToken);

        return run;
    }

    /// <summary>
    /// Reads execution state and orders jobs according to the restored plan.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The snapshot, or null if the run no longer exists.</returns>
    public async Task<PipelineExecutionSnapshot?> GetExecutionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var snapshot = await executionStore.LoadAsync(id, cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        var specification = await store.GetSpecificationAsync(id, cancellationToken);

        if (specification is null)
        {
            // Reset may have deleted the run after its snapshot was loaded.
            var currentRun = await store.GetAsync(id, cancellationToken);

            if (currentRun is null)
            {
                return null;
            }

            throw new InvalidOperationException(
                $"Run '{id}' has no execution specification.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();

        var definitions = scope.ServiceProvider
            .GetRequiredService<IPipelineDefinitionRegistry>();

        var plan = definitions.Restore(specification);

        var jobOrder = plan.Jobs
            .Select((job, index) => new { job.Id, Index = index })
            .ToDictionary(
                job => job.Id,
                job => job.Index,
                StringComparer.Ordinal);

        var orderedJobs = snapshot.Jobs
            .OrderBy(job => jobOrder.TryGetValue(job.JobId, out var index)
                ? index
                : int.MaxValue)
            .ThenBy(job => job.JobId, StringComparer.Ordinal)
            .ToArray();

        return snapshot with
        {
            Jobs = orderedJobs
        };
    }

    /// <summary>
    /// Reads one run's metadata and ordered logs.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The run, or null if it no longer exists.</returns>
    public Task<PipelineRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return store.GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Reads a page of runs ordered by creation time descending, then identifier.
    /// </summary>
    /// <param name="skip">The nonnegative number of runs to skip.</param>
    /// <param name="take">The positive maximum number of runs to return.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The requested page of runs and their logs.</returns>
    public Task<IReadOnlyList<PipelineRun>> GetRunsAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return store.ListAsync(skip, take, cancellationToken);
    }

    /// <summary>
    /// Runs the built-in discovery and execution loop until host shutdown; it can be started only once per runtime instance.
    /// </summary>
    /// <param name="stoppingToken">The host shutdown token used to stop processing.</param>
    /// <returns>A task representing the lifetime of the processing loop.</returns>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        if (Interlocked.Exchange(ref _runnerStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The pipeline runner has already been started.");
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var runIds =
                        await executionStore.ListActiveRunIdsAsync(stoppingToken);

                    foreach (var runId in runIds)
                    {
                        try
                        {
                            await using var scope = scopeFactory.CreateAsyncScope();

                            var coordinator = scope.ServiceProvider.GetRequiredService<PipelineExecutionCoordinator>();

                            // Advance several immediate transitions,
                            // then give other runs a turn.
                            for (var transition = 0;
                                 transition < 100;
                                 transition++)
                            {
                                if (!await coordinator.AdvanceAsync(runId, stoppingToken))
                                {
                                    break;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                            when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(
                                exception,
                                "Could not advance pipeline run {RunId}.",
                                runId);
                        }
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Could not discover pipeline work.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    timeProvider,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Unfinished work remains in SQL for recovery.
        }
    }

    /// <summary>
    /// Reopens a failed run for unfinished work, preserving successful steps and outputs, skipped jobs, bound arguments, and logs.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if reopened; false if missing, no longer failed, or changed concurrently.</returns>
    /// <exception cref="InvalidOperationException">A step is still running or the original specification cannot be restored.</exception>
    public async Task<bool> RetryAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var retry = scope.ServiceProvider
            .GetRequiredService<PipelineRetryService>();

        return await retry.RetryAsync(runId, cancellationToken);
    }
}