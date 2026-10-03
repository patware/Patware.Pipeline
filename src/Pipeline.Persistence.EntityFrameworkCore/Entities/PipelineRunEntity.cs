using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore.Entities;

internal sealed class PipelineRunEntity
{
    public Guid Id { get; set; }

    public required string DefinitionId { get; set; }
    public required string DefinitionDisplayName { get; set; }
    public int DefinitionVersion { get; set; }

    public required string Title { get; set; }
    public required string CreatedBy { get; set; }

    public long Revision { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public PipelineStatus Status { get; set; }
    public string StatusText { get; set; } = "";

    public List<PipelineLogEntryEntity> Logs { get; set; } = [];
}
