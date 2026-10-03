using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore.Entities;

internal sealed class PipelineJobEntity
{
    public Guid RunId { get; set; }

    public required string JobId { get; set; }

    public JobExecutionStatus Status { get; set; }

    public long Revision { get; set; }

    public bool? ConditionResult { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public string? Error { get; set; }
}