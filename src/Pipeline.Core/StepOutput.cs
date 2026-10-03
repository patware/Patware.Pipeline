namespace Pipeline.Core;

/// <summary>
/// Provides a typed reference to an output producer within a single builder; it does not contain the result value.
/// </summary>
/// <remarks>Pass this handle to a step's Using method or <see cref="IJobBuilder.When{TOutput}" />. It cannot be reused with another builder.</remarks>
/// <seealso cref="IStepBuilder{TStep}.Produces{TResult}" />
/// <typeparam name="T">The producer result type referenced by this handle.</typeparam>
public sealed class StepOutput<T>
{
    internal StepOutput(Guid builderId, string jobId, string stepId)
    {
        BuilderId = builderId;
        JobId = jobId;
        StepId = stepId;
    }

    internal Guid BuilderId { get; private set; }
    /// <summary>
    /// Gets the job identifier, scoped to its pipeline plan or run.
    /// </summary>
    public string JobId { get; private set; }
    /// <summary>
    /// Gets the step identifier, scoped to its containing job.
    /// </summary>
    public string StepId { get; private set; }
}
