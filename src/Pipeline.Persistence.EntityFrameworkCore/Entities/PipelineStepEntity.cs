using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore.Entities;

internal sealed class PipelineStepEntity
{
    public Guid RunId { get; set; }

    public required string JobId { get; set; }
    public required string StepId { get; set; }

    public StepExecutionStatus Status { get; set; }

    public long Revision { get; set; }
    public int Attempt { get; set; }

    public string? ArgumentsJson { get; set; }

    public bool HasOutput { get; set; }
    public string? OutputJson { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? PollDeadline { get; set; }

    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public string? Error { get; set; }
}