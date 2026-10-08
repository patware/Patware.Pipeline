namespace Pipeline.Contracts;

/// <summary>
/// Contains the metadata and job statuses needed by the run overview.
/// </summary>
/// <remarks>
/// Excludes logs and step execution details.
/// </remarks>
public sealed record PipelineRunSummaryView
{
    /// <summary>
    /// Gets the run identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the definition associated with the run.
    /// </summary>
    public required PipelineDefinitionView Definition { get; init; }

    /// <summary>
    /// Gets the run's display title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the recorded submitter identity.
    /// </summary>
    public required string CreatedBy { get; init; }

    /// <summary>
    /// Gets the time the run was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the current run status.
    /// </summary>
    public PipelineStatus Status { get; init; }

    /// <summary>
    /// Gets the human-readable status description.
    /// </summary>
    public string StatusText { get; init; } = "";

    /// <summary>
    /// Gets the jobs in display order.
    /// </summary>
    public IReadOnlyList<PipelineJobView> Jobs { get; init; } = [];
}