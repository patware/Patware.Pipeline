using System.Text.Json;

using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Advances persisted run state, evaluates dependencies and conditions, and manages step invocation leases.
/// </summary>
/// <remarks>
/// A run has at most one live invocation. Revision checks and renewable leases fence stale writes, while the operation gate serializes local work.
/// An expired lease can be reclaimed and a failed step can be retried, so step methods should tolerate repeated invocation and partial external side effects.
/// Use <see cref="PrepareNextStepAsync" /> and <see cref="ExecuteStepAsync" /> for separate dispatch, or <see cref="AdvanceAsync" /> for the built-in worker.
/// </remarks>
/// <param name="pipelineStore">The store supplying run metadata and restoration specifications.</param>
/// <param name="executionStore">The store used to load snapshots and commit revision-checked execution transitions.</param>
/// <param name="definitions">The registry restoring the original versioned execution plan.</param>
/// <param name="binder">The binder resolving and serializing step arguments.</param>
/// <param name="invoker">The invoker resolving and calling step services.</param>
/// <param name="timeProvider">The clock used for timestamps, polling deadlines, or lease validity.</param>
/// <param name="operationGate">The shared process-local gate coordinating execution with reset, retry, and submission.</param>
public sealed class PipelineExecutionCoordinator(
    IPipelineStore pipelineStore,
    IPipelineExecutionStore executionStore,
    IPipelineDefinitionRegistry definitions,
    IStepArgumentBinder binder,
    IStepInvoker invoker,
    TimeProvider timeProvider,
    PipelineOperationGate operationGate)
{
    private static readonly TimeSpan LeaseDuration =
        TimeSpan.FromMinutes(2);

    private static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Attempts one run transition, invoking an eligible step when appropriate.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if a transition was persisted; false if no work is ready, the run is terminal or missing, or ownership was lost.</returns>
    public Task<bool> AdvanceAsync(Guid runId, CancellationToken cancellationToken)
    {
        return operationGate.ExecuteAsync(
            () => AdvanceCoreAsync(runId, cancellationToken),
            cancellationToken);
    }
    /// <summary>
    /// Attempts a coordination transition or selects an eligible step for separate dispatch without invoking it.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The transition outcome and optional work item; selection alone does not claim the step.</returns>
    public Task<PipelineAdvanceResult> PrepareNextStepAsync(
    Guid runId,
    CancellationToken cancellationToken)
    {
        return operationGate.ExecuteAsync(async () =>
        {
            PipelineStepWorkItem? selected = null;

            var progressed = await AdvanceCoreAsync(
                runId,
                cancellationToken,
                selectStep: work => selected = work);

            return new PipelineAdvanceResult(progressed, selected);
        }, cancellationToken);
    }

    /// <summary>
    /// Attempts the specified step only when its expected attempt still matches persisted state and no live invocation owns the run.
    /// </summary>
    /// <param name="work">The dispatched step identity and expected attempt number.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>True if progress was saved; false for stale dispatch, unavailable work, or a failed ownership or revision check.</returns>
    public Task<bool> ExecuteStepAsync(
        PipelineStepWorkItem work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentException.ThrowIfNullOrWhiteSpace(work.JobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(work.StepId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            work.ExpectedAttempt);

        return operationGate.ExecuteAsync(
            () => AdvanceCoreAsync(
                work.RunId,
                cancellationToken,
                requiredStep: work),
            cancellationToken);
    }

    private async Task<bool> AdvanceCoreAsync(
        Guid runId,
        CancellationToken cancellationToken,
        Action<PipelineStepWorkItem>? selectStep = null,
        PipelineStepWorkItem? requiredStep = null)
    {
        var snapshot = await executionStore.LoadAsync(runId, cancellationToken);

        if (snapshot is null || IsTerminal(snapshot.Run.Status))
        {
            return false;
        }

        if (requiredStep is not null)
        {
            var targetJob = snapshot.Jobs.SingleOrDefault(job =>
                job.JobId == requiredStep.JobId);

            var targetStep = snapshot.Steps.SingleOrDefault(step =>
                step.JobId == requiredStep.JobId &&
                step.StepId == requiredStep.StepId);

            if (snapshot.Run.Status != PipelineStatus.Running ||
                targetJob is null ||
                targetJob.Status != JobExecutionStatus.Running ||
                targetStep is null)
            {
                return false;
            }

            if (targetStep.Status is not (
                StepExecutionStatus.Pending or
                StepExecutionStatus.Waiting or
                StepExecutionStatus.Running))
            {
                return false;
            }

            if (requiredStep.ExpectedAttempt !=
                checked(targetStep.Attempt + 1))
            {
                // This dispatch belongs to an earlier or invalid attempt.
                return false;
            }

            if (snapshot.Steps.Any(step => step.Status is
                StepExecutionStatus.Failed or
                StepExecutionStatus.TimedOut or
                StepExecutionStatus.Cancelled))
            {
                // Let run coordination propagate the failure.
                return false;
            }
        }

        var specification = await pipelineStore.GetSpecificationAsync(
            runId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Run '{runId}' has no execution specification.");

        var plan = definitions.Restore(specification);
        var now = timeProvider.GetUtcNow();

        // One live invocation per run in this first implementation.
        if (snapshot.Steps.Any(step =>
            step.Status == StepExecutionStatus.Running &&
            step.LeaseExpiresAt > now))
        {
            return false;
        }

        if (snapshot.Run.Status == PipelineStatus.Queued)
        {
            return await SaveAsync(
                snapshot with
                {
                    Run = snapshot.Run with
                    {
                        Status = PipelineStatus.Running,
                        StartedAt = now,
                        StatusText = "Pipeline execution started."
                    }
                },
                "Pipeline execution started.",
                cancellationToken);
        }

        // Finish failure propagation before attempting more work.
        var failedStep = snapshot.Steps.FirstOrDefault(step =>
            step.Status is
                StepExecutionStatus.Failed or
                StepExecutionStatus.TimedOut or
                StepExecutionStatus.Cancelled);

        if (failedStep is not null)
        {
            var message =
                $"Step '{failedStep.JobId}/{failedStep.StepId}' " +
                $"ended as {failedStep.Status}: {failedStep.Error}";

            var jobs = snapshot.Jobs.Select(job =>
            {
                if (IsSuccessful(job.Status))
                {
                    return job;
                }

                var status = job.JobId == failedStep.JobId
                    ? failedStep.Status switch
                    {
                        StepExecutionStatus.TimedOut => JobExecutionStatus.TimedOut,
                        StepExecutionStatus.Cancelled => JobExecutionStatus.Cancelled,
                        _ => JobExecutionStatus.Failed
                    }
                    : JobExecutionStatus.Blocked;

                return job with
                {
                    Status = status,
                    FinishedAt = now,
                    Error = message
                };
            }).ToArray();

            return await SaveAsync(
                snapshot with
                {
                    Jobs = jobs,
                    Run = snapshot.Run with
                    {
                        Status = PipelineStatus.Failed,
                        FinishedAt = now,
                        StatusText = message
                    }
                },
                message,
                cancellationToken);
        }

        if (snapshot.Jobs.All(job => IsSuccessful(job.Status)))
        {
            return await SaveAsync(
                snapshot with
                {
                    Run = snapshot.Run with
                    {
                        Status = PipelineStatus.Completed,
                        FinishedAt = now,
                        StatusText = "Pipeline execution completed."
                    }
                },
                "Pipeline execution completed.",
                cancellationToken);
        }

        foreach (var jobDefinition in plan.Jobs)
        {
            // Restrict targeted execution to its requested job and step
            if (requiredStep is not null && jobDefinition.Id != requiredStep.JobId)
            {
                continue;
            }

            var job = snapshot.Jobs.Single(candidate => candidate.JobId == jobDefinition.Id);

            if (IsSuccessful(job.Status))
            {
                continue;
            }

            var dependencies = jobDefinition.Dependencies
                .Select(dependency => (
                    Dependency: dependency,
                    State: snapshot.Jobs.Single(
                        candidate =>
                            candidate.JobId == dependency.JobId)))
                .ToArray();

            var blocked = dependencies.Any(item =>
                item.State.Status is
                    JobExecutionStatus.Failed or
                    JobExecutionStatus.TimedOut or
                    JobExecutionStatus.Cancelled or
                    JobExecutionStatus.Blocked ||
                item.State.Status == JobExecutionStatus.ConditionSkipped &&
                !item.Dependency.AllowConditionSkipped);

            if (blocked)
            {
                var message = $"Job '{job.JobId}' is blocked by a prerequisite.";

                return await SaveAsync(
                    ReplaceJob(snapshot, job with
                    {
                        Status = JobExecutionStatus.Blocked,
                        FinishedAt = now,
                        Error = message
                    }) with
                    {
                        Run = snapshot.Run with
                        {
                            Status = PipelineStatus.Failed,
                            FinishedAt = now,
                            StatusText = message
                        }
                    },
                    message,
                    cancellationToken,
                    jobId: job.JobId);
            }

            var ready = dependencies.All(item =>
                item.State.Status == JobExecutionStatus.Succeeded ||
                item.State.Status == JobExecutionStatus.ConditionSkipped &&
                item.Dependency.AllowConditionSkipped);

            if (!ready)
            {
                continue;
            }

            if (job.Status == JobExecutionStatus.Pending)
            {
                bool shouldRun;

                try
                {
                    shouldRun = jobDefinition.Condition is null ||
                        EvaluateCondition(
                            jobDefinition.Condition,
                            snapshot);
                }
                catch (Exception exception)
                {
                    var message =
                        $"Condition for '{job.JobId}' failed: " +
                        exception.Message;

                    return await SaveAsync(
                        ReplaceJob(snapshot, job with
                        {
                            Status = JobExecutionStatus.Failed,
                            FinishedAt = now,
                            Error = message
                        }) with
                        {
                            Run = snapshot.Run with
                            {
                                Status = PipelineStatus.Failed,
                                FinishedAt = now,
                                StatusText = message
                            }
                        },
                        message,
                        cancellationToken,
                        jobId: job.JobId);
                }

                return await SaveAsync(
                    ReplaceJob(snapshot, job with
                    {
                        Status = shouldRun
                            ? JobExecutionStatus.Running
                            : JobExecutionStatus.ConditionSkipped,
                        ConditionResult = shouldRun,
                        StartedAt = shouldRun ? now : null,
                        FinishedAt = shouldRun ? null : now
                    }),
                    shouldRun
                        ? $"Job '{job.JobId}' started."
                        : $"Job '{job.JobId}' condition-skipped.",
                    cancellationToken,
                    jobId: job.JobId);
            }

            var nextDefinition = jobDefinition.Steps.FirstOrDefault(
                definition => snapshot.Steps.Single(step =>
                    step.JobId == job.JobId &&
                    step.StepId == definition.Id).Status
                        != StepExecutionStatus.Succeeded);

            if (nextDefinition is null)
            {
                return await SaveAsync(
                    ReplaceJob(snapshot, job with
                    {
                        Status = JobExecutionStatus.Succeeded,
                        FinishedAt = now
                    }),
                    $"Job '{job.JobId}' completed.",
                    cancellationToken,
                    jobId: job.JobId);
            }

            var next = snapshot.Steps.Single(step =>
                step.JobId == job.JobId &&
                step.StepId == nextDefinition.Id);

            if (requiredStep is not null && next.StepId != requiredStep.StepId)
            {
                // The requested step is no longer the next step in this job.
                return false;
            }

            if (next.PollDeadline is { } deadline && deadline <= now)
            {
                return await SaveAsync(
                    ReplaceStep(snapshot, next with
                    {
                        Status = StepExecutionStatus.TimedOut,
                        FinishedAt = now,
                        LeaseToken = null,
                        LeaseExpiresAt = null,
                        Error = "Polling deadline reached."
                    }),
                    $"Step '{job.JobId}/{next.StepId}' timed out.",
                    cancellationToken,
                    jobId: job.JobId,
                    stepId: next.StepId);
            }

            if (next.Status == StepExecutionStatus.Waiting &&
                next.NextAttemptAt > now)
            {
                continue;
            }

            if (selectStep is not null)
            {
                selectStep(new PipelineStepWorkItem(
                    snapshot.Run.Id,
                    snapshot.Run.Title,
                    job.JobId,
                    next.StepId,
                    checked(next.Attempt + 1)));

                // Selection alone does not change execution state.
                return false;
            }

            var token = Guid.NewGuid();

            var claimed = next with
            {
                Status = StepExecutionStatus.Running,
                Attempt = checked(next.Attempt + 1),
                StartedAt = next.StartedAt ?? now,
                NextAttemptAt = null,
                LeaseToken = token,
                LeaseExpiresAt = now + LeaseDuration,
                PollDeadline = next.PollDeadline ??
                    (nextDefinition.Poll is { } poll
                        ? now + poll.Timeout
                        : null)
            };

            var acquired = await SaveAsync(
                ReplaceStep(snapshot, claimed),
                $"Step '{job.JobId}/{next.StepId}' started.",
                cancellationToken,
                jobId: job.JobId,
                stepId: next.StepId);

            if (!acquired)
            {
                return false;
            }

            var claim = new StepClaim(
                runId,
                job.JobId,
                next.StepId,
                token,
                claimed.LeaseExpiresAt!.Value);

            await ExecuteAsync(
                nextDefinition,
                claimed,
                claim,
                cancellationToken);

            return true;
        }

        return false;
    }

    private static bool EvaluateCondition(
        ConditionDefinition condition,
        PipelineExecutionSnapshot snapshot)
    {
        var source = snapshot.Steps.Single(step =>
            step.JobId == condition.Source.JobId &&
            step.StepId == condition.Source.StepId);

        if (source.Status != StepExecutionStatus.Succeeded ||
            !source.HasOutput ||
            source.OutputJson is null)
        {
            throw new InvalidOperationException("The condition's producer output is unavailable.");
        }

        ArgumentExpressionValidator.ValidateCondition(condition.Predicate);

        var output = JsonSerializer.Deserialize(source.OutputJson, condition.Source.ValueType);

        return condition.Predicate.Compile().DynamicInvoke(output)
            is bool result
                ? result
                : throw new InvalidOperationException(
                    "The condition did not return a Boolean.");
    }

    private async Task ExecuteAsync(
        StepDefinition definition,
        StepExecutionState initialState,
        StepClaim claim,
        CancellationToken stoppingToken)
    {
        using var leaseLost = new CancellationTokenSource();

        using var invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, leaseLost.Token);

        if (initialState.PollDeadline is { } deadline)
        {
            var remaining = deadline - timeProvider.GetUtcNow();

            if (remaining <= TimeSpan.Zero)
            {
                invocationCancellation.Cancel();
            }
            else
            {
                invocationCancellation.CancelAfter(remaining);
            }
        }

        using var renewalStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var renewal = RenewAsync(
            claim,
            renewalStop.Token,
            leaseLost);

        StepInvocationResult? result = null;
        Exception? failure = null;

        try
        {
            var arguments = initialState.ArgumentsJson is not null
                ? new BoundInvocation(initialState.ArgumentsJson)
                : await binder.BindAsync(
                    claim.RunId,
                    definition,
                    invocationCancellation.Token);

            if (initialState.ArgumentsJson is null)
            {
                var saved = await EditOwnedStepAsync(
                    claim,
                    step => step with
                    {
                        ArgumentsJson = arguments.ArgumentsJson
                    },
                    $"Arguments saved for '{claim.JobId}/{claim.StepId}'.",
                    invocationCancellation.Token);

                if (!saved)
                {
                    leaseLost.Cancel();
                    return;
                }
            }

            result = await invoker.InvokeAsync(
                definition,
                arguments,
                claim,
                invocationCancellation.Token);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            renewalStop.Cancel();

            try
            {
                await renewal;
            }
            catch (OperationCanceledException)
                when (renewalStop.IsCancellationRequested)
            {
                // Normal end of lease renewal.
            }
        }

        // Leave the saved claim to expire on interruption or ownership loss.
        if (stoppingToken.IsCancellationRequested ||
            leaseLost.IsCancellationRequested)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        var timedOut = initialState.PollDeadline is { } end && now >= end;

        await EditOwnedStepAsync(
            claim,
            step =>
            {
                if (timedOut || failure is not null)
                {
                    return step with
                    {
                        Status = timedOut
                            ? StepExecutionStatus.TimedOut
                            : StepExecutionStatus.Failed,
                        FinishedAt = now,
                        LeaseToken = null,
                        LeaseExpiresAt = null,
                        Error = timedOut
                            ? "Polling deadline reached."
                            : failure!.Message
                    };
                }

                if (definition.Kind == StepKind.Poll &&
                    result!.PollSatisfied == false)
                {
                    var proposed = now + definition.Poll!.Every;
                    var deadline = step.PollDeadline!.Value;

                    return step with
                    {
                        Status = StepExecutionStatus.Waiting,
                        NextAttemptAt = proposed < deadline
                            ? proposed
                            : deadline,
                        LeaseToken = null,
                        LeaseExpiresAt = null
                    };
                }

                return step with
                {
                    Status = StepExecutionStatus.Succeeded,
                    FinishedAt = now,
                    HasOutput = result!.HasOutput,
                    OutputJson = result.OutputJson,
                    LeaseToken = null,
                    LeaseExpiresAt = null,
                    Error = null
                };
            },
            timedOut
                ? $"Step '{claim.JobId}/{claim.StepId}' timed out."
                : failure is not null
                    ? $"Step '{claim.JobId}/{claim.StepId}' failed: " +
                      failure.Message
                    : definition.Kind == StepKind.Poll &&
                      result!.PollSatisfied == false
                        ? $"Step '{claim.JobId}/{claim.StepId}' is waiting."
                        : $"Step '{claim.JobId}/{claim.StepId}' completed.",
            stoppingToken);
    }

    private async Task RenewAsync(
        StepClaim claim,
        CancellationToken cancellationToken,
        CancellationTokenSource leaseLost)
    {
        try
        {
            while (true)
            {
                await Task.Delay(
                    RenewalInterval,
                    timeProvider,
                    cancellationToken);

                var renewed = await EditOwnedStepAsync(
                    claim,
                    step => step with
                    {
                        LeaseExpiresAt =
                            timeProvider.GetUtcNow() + LeaseDuration
                    },
                    $"Lease renewed for '{claim.JobId}/{claim.StepId}'.",
                    cancellationToken);

                if (!renewed)
                {
                    leaseLost.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown of the renewal loop.
        }
        catch
        {
            // Without renewal confirmation, stop trusting ownership.
            leaseLost.Cancel();
        }
    }

    private async Task<bool> EditOwnedStepAsync(
    StepClaim claim,
    Func<StepExecutionState, StepExecutionState> edit,
    string message,
    CancellationToken cancellationToken,
    PipelineLogLevel level = PipelineLogLevel.Information)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var snapshot = await executionStore.LoadAsync(
                claim.RunId,
                cancellationToken);

            if (snapshot is null)
            {
                return false;
            }

            var step = snapshot.Steps.Single(candidate =>
                candidate.JobId == claim.JobId &&
                candidate.StepId == claim.StepId);

            if (step.Status != StepExecutionStatus.Running ||
                step.LeaseToken != claim.LeaseToken ||
                step.LeaseExpiresAt is null ||
                step.LeaseExpiresAt <= timeProvider.GetUtcNow())
            {
                return false;
            }

            var saved = await executionStore.TrySaveAsync(
                replacement: ReplaceStep(snapshot, edit(step)),
                expectedRunRevision: snapshot.Run.Revision,
                message: message,
                requiredClaim: claim,
                cancellationToken: cancellationToken,
                level: level,
                jobId: claim.JobId,
                stepId: claim.StepId);

            if (saved)
            {
                return true;
            }
        }

        return false;
    }

    private Task<bool> SaveAsync(
        PipelineExecutionSnapshot snapshot,
        string message,
        CancellationToken cancellationToken,
        PipelineLogLevel level = PipelineLogLevel.Information,
        string? jobId = null,
        string? stepId = null)
    {
        return executionStore.TrySaveAsync(
            snapshot,
            snapshot.Run.Revision,
            message,
            requiredClaim: null,
            cancellationToken: cancellationToken,
            level: level,
            jobId: jobId,
            stepId: stepId);
    }

    private static PipelineExecutionSnapshot ReplaceJob(PipelineExecutionSnapshot snapshot, JobExecutionState replacement)
    {
        return snapshot with
        {
            Jobs = snapshot.Jobs
                .Select(job => job.JobId == replacement.JobId
                    ? replacement
                    : job)
                .ToArray()
        };
    }

    private static PipelineExecutionSnapshot ReplaceStep(PipelineExecutionSnapshot snapshot, StepExecutionState replacement)
    {
        return snapshot with
        {
            Steps = snapshot.Steps
                .Select(step =>
                    step.JobId == replacement.JobId &&
                    step.StepId == replacement.StepId
                        ? replacement
                        : step)
                .ToArray()
        };
    }

    private static bool IsSuccessful(JobExecutionStatus status)
    {
        return status is JobExecutionStatus.Succeeded or JobExecutionStatus.ConditionSkipped;
    }

    private static bool IsTerminal(PipelineStatus status)
    {
        return status is PipelineStatus.Completed or PipelineStatus.Failed or PipelineStatus.Cancelled;
    }
}