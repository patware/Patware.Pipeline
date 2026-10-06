using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Holds attribution metadata for one step invocation scope.
/// </summary>
public sealed class PipelineStepContext : IPipelineStepContext
{
    private PipelineStepContextSnapshot? _current;

    /// <summary>
    /// Gets the current step context snapshot for this invocation.
    /// </summary>
    public PipelineStepContextSnapshot Current =>
        _current ?? throw new InvalidOperationException("Pipeline context requires an active step invocation.");

    internal void Initialize(PipelineRun run, StepClaim claim)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(claim);

        if (_current is not null)
        {
            throw new InvalidOperationException("This context already belongs to a step invocation.");
        }

        if (run.Id != claim.RunId)
        {
            throw new InvalidOperationException("The run and step claim do not match.");
        }

        _current = new PipelineStepContextSnapshot(
            run.Id,
            run.Definition,
            run.Title,
            run.CreatedBy,
            run.CreatedAt,
            claim.JobId,
            claim.StepId);
    }
}