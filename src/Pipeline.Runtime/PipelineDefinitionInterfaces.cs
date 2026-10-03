using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Restores one definition version's executable plan from serialized input and settings.
/// </summary>
/// <remarks>
/// Register implementations in dependency injection as <see cref="IPipelineDefinitionRegistration" />.
/// Deserialize the specification's input and settings and rebuild the original graph with stable job and step IDs.
/// Keep older versions registered while stored runs still reference them; expression trees are not persisted.
/// </remarks>
/// <seealso cref="IPipelineDefinitionRegistry" />
public interface IPipelineDefinitionRegistration
{
    /// <summary>
    /// Gets the stable pipeline definition identifier used for versioned restoration.
    /// </summary>
    string DefinitionId { get; }

    /// <summary>
    /// Gets the definition version required to reconstruct the original execution plan.
    /// </summary>
    int DefinitionVersion { get; }

    /// <summary>
    /// Reconstructs an executable plan using the specification's exact definition ID, version, input, and settings.
    /// </summary>
    /// <param name="specification">The persisted definition identity, version, and serialized input and settings.</param>
    /// <returns>The plan compatible with the persisted definition version.</returns>
    /// <exception cref="InvalidOperationException">The definition version is unavailable or the specification is incompatible.</exception>
    PipelinePlan Restore(PipelineExecutionSpecification specification);
}

/// <summary>
/// Resolves the registration for a persisted definition ID and version to reconstruct its plan.
/// </summary>
public interface IPipelineDefinitionRegistry
{
    /// <summary>
    /// Reconstructs an executable plan using the specification's exact definition ID, version, input, and settings.
    /// </summary>
    /// <param name="specification">The persisted definition identity, version, and serialized input and settings.</param>
    /// <returns>The plan compatible with the persisted definition version.</returns>
    /// <exception cref="InvalidOperationException">The definition version is unavailable or the specification is incompatible.</exception>
    PipelinePlan Restore(PipelineExecutionSpecification specification);
}