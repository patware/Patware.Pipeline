namespace Pipeline.Core;

/// <summary>
/// Provides attribution metadata for the current step invocation.
/// </summary>
public interface IPipelineStepContext
{
    /// <summary>
    /// Gets the current step context snapshot for this invocation.
    /// </summary>
    PipelineStepContextSnapshot Current { get; }
}

/// <summary>
/// Identifies the run and step responsible for an operation.
/// </summary>
public sealed record PipelineStepContextSnapshot(
    Guid RunId,
    PipelineDefinition Definition,
    string Title,
    string CreatedBy,
    DateTimeOffset RunCreatedAt,
    string JobId,
    string StepId);