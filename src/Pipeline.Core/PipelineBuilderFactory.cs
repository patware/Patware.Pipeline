namespace Pipeline.Core;

/// <summary>
/// Creates graph builders after validating the definition identity, display name, and positive version.
/// </summary>
public sealed class PipelineBuilderFactory : IPipelineBuilderFactory
{
    /// <summary>
    /// Creates a builder for the supplied definition.
    /// </summary>
    /// <param name="definition">The versioned pipeline definition associated with this plan or run.</param>
    /// <returns>A new, independently mutable pipeline builder.</returns>
    public IPipelineBuilder Create(PipelineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.DisplayName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            definition.Version);

        return new PipelineGraphBuilder(definition);
    }
}