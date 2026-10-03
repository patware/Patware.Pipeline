using System.Text.Json;

using Pipeline.Core;
using Pipeline.Core.Pipelines;

namespace Pipeline.Runtime.Pipelines;

/// <summary>
/// Restores the log formatting demonstration from its versioned persisted specification.
/// </summary>
/// <param name="pipeline">The log formatting pipeline whose plan is reconstructed.</param>
public sealed class LogFormattingRegistration(LogFormattingPipeline pipeline) : IPipelineDefinitionRegistration
{
    /// <summary>
    /// Gets the stable pipeline definition identifier used for versioned restoration.
    /// </summary>
    public string DefinitionId => LogFormattingPipeline.Definition.Id;

    /// <summary>
    /// Gets the definition version required to reconstruct the original execution plan.
    /// </summary>
    public int DefinitionVersion => LogFormattingPipeline.Definition.Version;

    /// <summary>
    /// Reconstructs an executable plan using the specification's exact definition ID, version, input, and settings.
    /// </summary>
    /// <param name="specification">The persisted definition identity, version, and serialized input and settings.</param>
    /// <returns>The plan compatible with the persisted definition version.</returns>
    /// <exception cref="InvalidOperationException">The definition version is unavailable or the specification is incompatible.</exception>
    public PipelinePlan Restore(PipelineExecutionSpecification specification)
    {
        if (specification.DefinitionId != DefinitionId ||
            specification.DefinitionVersion != DefinitionVersion)
        {
            throw new InvalidOperationException(
                "The specification does not match this registration.");
        }

        _ = JsonSerializer.Deserialize<LogFormattingInput>(
            specification.InputJson)
            ?? throw new InvalidOperationException(
                "Pipeline inputs are missing.");

        _ = JsonSerializer.Deserialize<LogFormattingSettings>(
            specification.SettingsJson)
            ?? throw new InvalidOperationException(
                "Pipeline settings are missing.");

        return pipeline.CreatePlan();
    }
}