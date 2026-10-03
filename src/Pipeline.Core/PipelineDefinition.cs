namespace Pipeline.Core
{
    /// <summary>
    /// Identifies a pipeline definition and its version independently of any particular run.
    /// </summary>
    /// <param name="Id">The identifier of this object within its containing pipeline context.</param>
    /// <param name="DisplayName">The human-readable name displayed for the pipeline definition.</param>
    /// <param name="Version">The positive definition version used to select a compatible plan-restoration registration.</param>
    public sealed record PipelineDefinition
    (
        string Id,
        string DisplayName,
        int Version = 1);
}
