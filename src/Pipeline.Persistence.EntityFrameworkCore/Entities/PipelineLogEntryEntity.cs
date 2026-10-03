using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore.Entities;

internal sealed class PipelineLogEntryEntity
{
    public Guid RunId { get; set; }
    public int Sequence { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public required string Message { get; set; }

    public PipelineLogLevel Level { get; set; } = PipelineLogLevel.Information;

    public string? JobId { get; set; }
    public string? StepId { get; set; }
}