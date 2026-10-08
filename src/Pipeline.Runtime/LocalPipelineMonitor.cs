namespace Pipeline.Runtime;

/// <summary>
/// Projects the selected runtime's state into monitoring contracts.
/// </summary>
/// <param name="runtime">
/// The selected runtime, including any scheduler-specific dispatch behaviour.
/// </param>
internal sealed class LocalPipelineMonitor(
    IPipelineRuntime runtime) : Contracts.IPipelineMonitor
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Contracts.PipelineRunSummaryView>> GetRunsAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var recentRuns = await runtime.GetRunsAsync(
            skip,
            take,
            cancellationToken);

        var summaries = new List<Contracts.PipelineRunSummaryView>(recentRuns.Count);

        foreach (var recentRun in recentRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = await runtime.GetExecutionAsync(
                recentRun.Id,
                cancellationToken);

            // A reset may remove a run after the list was loaded.
            if (snapshot is null)
            {
                continue;
            }

            var run = snapshot.Run;

            summaries.Add(new Contracts.PipelineRunSummaryView
            {
                Id = run.Id,
                Definition = new Contracts.PipelineDefinitionView(
                    run.Definition.Id,
                    run.Definition.DisplayName,
                    run.Definition.Version),
                Title = run.Title,
                CreatedBy = run.CreatedBy,
                CreatedAt = run.CreatedAt,
                Status = Enum.Parse<Contracts.PipelineStatus>(
                    run.Status.ToString()),
                StatusText = run.StatusText,
                Jobs = [.. snapshot.Jobs.Select(ToView)]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        return [.. summaries];
    }

    /// <inheritdoc />
    public async Task<Contracts.PipelineExecutionView?> GetExecutionAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await runtime.GetExecutionAsync(
            runId, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (snapshot is null)
        {
            return null;
        }

        return new Contracts.PipelineExecutionView(
            ToView(snapshot.Run),
            [.. snapshot.Jobs.Select(ToView)],
            [.. snapshot.Steps.Select(ToView)]);
    }

    /// <inheritdoc />
    public Task<bool> RetryAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return runtime.RetryAsync(runId, cancellationToken);
    }

    private static Contracts.PipelineRunView ToView(PipelineRun run)
    {
        return new Contracts.PipelineRunView
        {
            Id = run.Id,
            Definition = new Contracts.PipelineDefinitionView(
                run.Definition.Id,
                run.Definition.DisplayName,
                run.Definition.Version),
            Title = run.Title,
            CreatedBy = run.CreatedBy,
            CreatedAt = run.CreatedAt,
            QueuedAt = run.QueuedAt,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            Status = Enum.Parse<Contracts.PipelineStatus>(
                run.Status.ToString()),
            StatusText = run.StatusText,
            Logs = [.. run.Logs.Select(ToView)]
        };
    }

    private static Contracts.PipelineJobView ToView(JobExecutionState job)
    {
        return new Contracts.PipelineJobView
        {
            RunId = job.RunId,
            JobId = job.JobId,
            Status = Enum.Parse<Contracts.JobExecutionStatus>(
                job.Status.ToString()),
            StartedAt = job.StartedAt,
            FinishedAt = job.FinishedAt
        };
    }

    private static Contracts.PipelineStepView ToView(StepExecutionState step)
    {
        return new Contracts.PipelineStepView
        {
            RunId = step.RunId,
            JobId = step.JobId,
            StepId = step.StepId,
            Status = Enum.Parse<Contracts.StepExecutionStatus>(
                step.Status.ToString()),
            NextAttemptAt = step.NextAttemptAt,
            PollDeadline = step.PollDeadline
        };
    }

    private static Contracts.PipelineLogEntryView ToView(
        PipelineLogEntry entry)
    {
        return new Contracts.PipelineLogEntryView(
            entry.Timestamp,
            entry.Message)
        {
            Level = Enum.Parse<Contracts.PipelineLogLevel>(
                entry.Level.ToString()),
            JobId = entry.JobId,
            StepId = entry.StepId
        };
    }
}