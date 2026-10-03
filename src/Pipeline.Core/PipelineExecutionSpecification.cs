namespace Pipeline.Core;

/// <summary>
/// Stores the versioned definition identity and serialized data needed to reconstruct a run's plan.
/// </summary>
/// <remarks>Expressions and service instances are not serialized. The runtime uses a registration for this exact ID and version to rebuild the graph.</remarks>
/// <seealso cref="PipelineDefinition" />
/// <seealso cref="PipelineRequest" />
/// <param name="DefinitionId">The stable pipeline definition identifier used for versioned restoration.</param>
/// <param name="DefinitionVersion">The definition version required to reconstruct the original execution plan.</param>
/// <param name="InputJson">The serialized pipeline input used when restoring the plan.</param>
/// <param name="SettingsJson">The serialized pipeline settings used when restoring the plan.</param>
public sealed record PipelineExecutionSpecification(
    string DefinitionId,
    int DefinitionVersion,
    string InputJson,
    string SettingsJson);