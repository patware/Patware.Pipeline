namespace Pipeline.Persistence.EntityFrameworkCore.Entities;

internal sealed class PipelineSpecificationEntity
{
    public Guid RunId { get; set; }

    public required string DefinitionId { get; set; }

    public int DefinitionVersion { get; set; }

    public required string InputJson { get; set; }

    public required string SettingsJson { get; set; }
}