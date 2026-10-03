using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Indexes definition registrations by ID and version and rejects duplicate registrations.
/// </summary>
public sealed class PipelineDefinitionRegistry : IPipelineDefinitionRegistry
{
    private readonly IReadOnlyDictionary<(string Id, int Version), IPipelineDefinitionRegistration> _registrations;

    /// <summary>
    /// Initializes the definition registry with uniquely versioned registrations.
    /// </summary>
    /// <param name="registrations">The available definition registrations, with unique ID and version pairs.</param>
    /// <exception cref="InvalidOperationException">Two registrations use the same definition ID and version.</exception>
    public PipelineDefinitionRegistry(IEnumerable<IPipelineDefinitionRegistration> registrations)
    {
        var dictionary = new Dictionary<(string Id, int Version), IPipelineDefinitionRegistration>();

        foreach (var registration in registrations)
        {
            var key = (
                registration.DefinitionId,
                registration.DefinitionVersion);

            if (!dictionary.TryAdd(key, registration))
            {
                throw new InvalidOperationException(
                    $"Duplicate pipeline definition '{key.Item1}' " +
                    $"version {key.Item2}.");
            }
        }

        _registrations = dictionary;
    }

    /// <summary>
    /// Reconstructs an executable plan using the specification's exact definition ID, version, input, and settings.
    /// </summary>
    /// <param name="specification">The persisted definition identity, version, and serialized input and settings.</param>
    /// <returns>The plan compatible with the persisted definition version.</returns>
    /// <exception cref="InvalidOperationException">The definition version is unavailable or the specification is incompatible.</exception>
    public PipelinePlan Restore(
        PipelineExecutionSpecification specification)
    {
        var key = (
            specification.DefinitionId,
            specification.DefinitionVersion);

        if (!_registrations.TryGetValue(key, out var registration))
        {
            throw new InvalidOperationException(
                $"Pipeline definition '{key.Item1}' " +
                $"version {key.Item2} is not registered.");
        }

        return registration.Restore(specification);
    }
}