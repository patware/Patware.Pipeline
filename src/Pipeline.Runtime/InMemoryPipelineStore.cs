using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Stores runs, specifications, execution state, and outputs in process memory using atomic updates.
/// </summary>
/// <remarks>All data is lost when the store instance or process is discarded. Recovery can only rediscover runs retained by this instance.</remarks>
/// <seealso cref="IPipelineStore" />
/// <seealso cref="IPipelineExecutionStore" />
/// <param name="timeProvider">The clock used for timestamps, polling deadlines, or lease validity.</param>
/// <param name="operationGate">The shared process-local gate coordinating execution with reset, retry, and submission.</param>
public sealed class InMemoryPipelineStore(
    TimeProvider timeProvider,
    PipelineOperationGate operationGate) :
    IPipelineStore,
    IPipelineExecutionStore,
    IStepOutputReader,
    IPipelineResetService
{
    private readonly Lock gate = new();

    private readonly Dictionary<Guid, Entry> entries = [];

    private sealed record Entry(PipelineExecutionSpecification Specification, PipelineExecutionSnapshot Snapshot);

    /// <summary>
    /// Atomically creates a revision-zero run, its restoration specification, and initial pending job and step states.
    /// </summary>
    /// <param name="run">The run metadata and ordered log history to persist.</param>
    /// <param name="request">The plan, serialized input and settings, and metadata to persist.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes when the initial data has been stored.</returns>
    public Task CreateAsync(PipelineRun run, PipelineRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);

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

        return AccessAsync(() =>
        {
            var specification = new PipelineExecutionSpecification(
                request.Definition.Id,
                request.Definition.Version,
                request.InputJson,
                request.SettingsJson);

            var jobs = request.Plan.Jobs
                .Select(job => new JobExecutionState
                {
                    RunId = run.Id,
                    JobId = job.Id,
                    Status = JobExecutionStatus.Pending,
                    Revision = 0
                })
                .ToArray();

            var steps = request.Plan.Jobs
                .SelectMany(job => job.Steps.Select(step =>
                    new StepExecutionState
                    {
                        RunId = run.Id,
                        JobId = job.Id,
                        StepId = step.Id,
                        Status = StepExecutionStatus.Pending,
                        Revision = 0,
                        Attempt = 0,
                        HasOutput = false
                    }))
                .ToArray();

            // Match the unique identities enforced by SQL persistence.
            if (jobs.Select(job => job.JobId).Distinct().Count()
                    != jobs.Length ||
                steps.Select(step => (step.JobId, step.StepId))
                    .Distinct().Count() != steps.Length)
            {
                throw new ArgumentException(
                    "The plan contains duplicate job or step identities.",
                    nameof(request));
            }

            var snapshot = new PipelineExecutionSnapshot(
                CopyRun(run),
                Array.AsReadOnly(jobs),
                Array.AsReadOnly(steps));

            // Add rejects duplicate run IDs. Nothing is stored before
            // the complete specification and snapshot are ready.
            entries.Add(run.Id, new Entry(specification, snapshot));

            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Reads run metadata and its ordered log history by identifier.
    /// </summary>
    /// <param name="id">The identifier of the pipeline run to query.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored run, or null if it does not exist.</returns>
    public Task<PipelineRun?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return AccessAsync<PipelineRun?>(() =>
            entries.TryGetValue(id, out var entry)
                ? entry.Snapshot.Run
                : null,
            cancellationToken);
    }

    /// <summary>
    /// Reads a page of runs ordered by creation time descending, then identifier.
    /// </summary>
    /// <param name="skip">The nonnegative number of runs to skip.</param>
    /// <param name="take">The positive maximum number of runs to return.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The requested page, including each run's log history.</returns>
    public Task<IReadOnlyList<PipelineRun>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        return AccessAsync<IReadOnlyList<PipelineRun>>(() =>
            Array.AsReadOnly(
                entries.Values
                    .Select(entry => entry.Snapshot.Run)
                    .OrderByDescending(run => run.CreatedAt)
                    .ThenBy(run => run.Id)
                    .Skip(skip)
                    .Take(take)
                    .ToArray()),
            cancellationToken);
    }

    /// <summary>
    /// Reads the versioned definition identity and serialized input and settings for plan restoration.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored specification, or null if it does not exist.</returns>
    public Task<PipelineExecutionSpecification?> GetSpecificationAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return AccessAsync<PipelineExecutionSpecification?>(() =>
            entries.TryGetValue(runId, out var entry)
                ? entry.Specification
                : null,
            cancellationToken);
    }

    /// <summary>
    /// Atomically replaces run metadata and appends logs when the stored revision matches the expected revision.
    /// </summary>
    /// <param name="run">The run metadata and ordered log history to persist.</param>
    /// <param name="expectedRevision">The stored run revision that must match; the replacement run must advance it by exactly one.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if saved; false if the run is missing or a concurrent writer changed its revision.</returns>
    /// <remarks>The replacement revision must be exactly one greater than the expected revision. Existing log entries must remain unchanged and in order.</remarks>
    public Task<bool> TryUpdateAsync(PipelineRun run, long expectedRevision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);

        if (run.Revision != checked(expectedRevision + 1))
        {
            throw new ArgumentException(
                "The replacement revision must advance by one.",
                nameof(run));
        }

        return AccessAsync(() =>
        {
            if (!entries.TryGetValue(run.Id, out var entry) ||
                entry.Snapshot.Run.Revision != expectedRevision)
            {
                return false;
            }

            var existingLogs = entry.Snapshot.Run.Logs;

            // Record equality includes severity and job/step scope.
            if (run.Logs.Count < existingLogs.Count ||
                !existingLogs.SequenceEqual(
                    run.Logs.Take(existingLogs.Count)))
            {
                throw new ArgumentException(
                    "Existing log entries cannot be changed, removed, " +
                    "or reordered.",
                    nameof(run));
            }

            entries[run.Id] = entry with
            {
                Snapshot = entry.Snapshot with
                {
                    Run = CopyRun(run)
                }
            };

            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Finds queued and running runs for processing or recovery.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The active run identifiers in queue order.</returns>
    public Task<IReadOnlyList<Guid>> ListActiveRunIdsAsync(CancellationToken cancellationToken)
    {
        return AccessAsync<IReadOnlyList<Guid>>(() =>
            Array.AsReadOnly(
                entries.Values
                    .Select(entry => entry.Snapshot.Run)
                    .Where(run => run.Status is
                        PipelineStatus.Queued or
                        PipelineStatus.Running)
                    .OrderBy(run => run.QueuedAt)
                    .ThenBy(run => run.Id)
                    .Select(run => run.Id)
                    .ToArray()),
            cancellationToken);
    }

    /// <summary>
    /// Loads a consistent view of the run and all its job and step execution states.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The execution snapshot, or null if the run does not exist.</returns>
    public Task<PipelineExecutionSnapshot?> LoadAsync(Guid runId, CancellationToken cancellationToken)
    {
        return AccessAsync<PipelineExecutionSnapshot?>(() =>
            entries.TryGetValue(runId, out var entry)
                ? entry.Snapshot
                : null,
            cancellationToken);
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
    public Task<bool> TrySaveAsync(
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
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRunRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        ValidateLogScope(replacement, requiredClaim, jobId, stepId);

        return AccessAsync(() =>
        {
            var runId = replacement.Run.Id;

            if (!entries.TryGetValue(runId, out var entry) ||
                entry.Snapshot.Run.Revision != expectedRunRevision)
            {
                return false;
            }

            var current = entry.Snapshot;
            var now = timeProvider.GetUtcNow();

            if (requiredClaim is not null)
            {
                var owned = current.Steps.SingleOrDefault(step =>
                    step.JobId == requiredClaim.JobId &&
                    step.StepId == requiredClaim.StepId);

                if (requiredClaim.RunId != runId ||
                    owned is null ||
                    owned.Status != StepExecutionStatus.Running ||
                    owned.LeaseToken != requiredClaim.LeaseToken ||
                    owned.LeaseExpiresAt is null ||
                    owned.LeaseExpiresAt <= now)
                {
                    return false;
                }
            }

            var replacementJobs = replacement.Jobs
                .ToDictionary(job => job.JobId);

            var replacementSteps = replacement.Steps
                .ToDictionary(step => (step.JobId, step.StepId));

            if (replacement.Jobs.Any(job => job.RunId != runId) ||
                replacement.Steps.Any(step => step.RunId != runId) ||
                current.Jobs.Count != replacementJobs.Count ||
                current.Steps.Count != replacementSteps.Count ||
                current.Jobs.Any(job =>
                    !replacementJobs.ContainsKey(job.JobId)) ||
                current.Steps.Any(step =>
                    !replacementSteps.ContainsKey(
                        (step.JobId, step.StepId))))
            {
                throw new InvalidOperationException(
                    "The replacement does not match the stored " +
                    "execution graph.");
            }

            // Match EF: revisions advance from stored values,
            // rather than trusting caller-supplied revisions.
            var jobs = current.Jobs
                .Select(job => replacementJobs[job.JobId] with
                {
                    Revision = checked(job.Revision + 1)
                })
                .ToArray();

            var steps = current.Steps
                .Select(step =>
                    replacementSteps[(step.JobId, step.StepId)] with
                    {
                        Revision = checked(step.Revision + 1)
                    })
                .ToArray();

            var log = new PipelineLogEntry(now, message)
            {
                Level = level,
                JobId = jobId,
                StepId = stepId
            };

            // Append to stored history. A supplied execution snapshot
            // must never replace or truncate existing logs.
            var run = current.Run with
            {
                Revision = checked(current.Run.Revision + 1),
                Status = replacement.Run.Status,
                StatusText = replacement.Run.StatusText,
                StartedAt = replacement.Run.StartedAt,
                FinishedAt = replacement.Run.FinishedAt,
                Logs = Array.AsReadOnly(
                    current.Run.Logs.Append(log).ToArray())
            };

            var snapshot = new PipelineExecutionSnapshot(
                run,
                Array.AsReadOnly(jobs),
                Array.AsReadOnly(steps));

            // Publish the complete update atomically.
            entries[runId] = entry with { Snapshot = snapshot };

            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Reads a step's producer success and output availability without invoking the step.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored output metadata, or null if the run or step does not exist.</returns>
    public Task<StoredStepOutput?> ReadAsync(Guid runId, string jobId, string stepId, CancellationToken cancellationToken)
    {
        return AccessAsync<StoredStepOutput?>(() =>
        {
            if (!entries.TryGetValue(runId, out var entry))
            {
                return null;
            }

            var step = entry.Snapshot.Steps.SingleOrDefault(candidate =>
                candidate.JobId == jobId &&
                candidate.StepId == stepId);

            return step is null
                ? null
                : new StoredStepOutput(
                    ProducerSucceeded:
                        step.Status == StepExecutionStatus.Succeeded,
                    HasOutput: step.HasOutput,
                    OutputJson: step.OutputJson);
        }, cancellationToken);
    }

    /// <summary>
    /// Deletes all runs and their specifications, job and step state, outputs, and logs.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The number of runs removed.</returns>
    public Task<int> ResetAsync(CancellationToken cancellationToken = default)
    {
        // Uses the current global gate, matching EfPipelineResetService.
        // Do not acquire this gate inside ordinary store writes:
        // the coordinator already holds it during step execution.
        return operationGate.ExecuteAsync(
            () => AccessAsync(() =>
            {
                var count = entries.Count;
                entries.Clear();
                return count;
            }, cancellationToken),
            cancellationToken);
    }

    private Task<T> AccessAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(operation());
        }
    }

    private static PipelineRun CopyRun(PipelineRun run)
    {
        return run with
        {
            Logs = Array.AsReadOnly(run.Logs.ToArray())
        };
    }

    private static void ValidateLogScope(
        PipelineExecutionSnapshot replacement,
        StepClaim? requiredClaim,
        string? jobId,
        string? stepId)
    {
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

        if (stepId is not null && !replacement.Steps.Any(step =>
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
    }
}