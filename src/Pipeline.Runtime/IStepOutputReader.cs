namespace Pipeline.Runtime;

/// <summary>
/// Separates producer completion and output availability from the serialized value, which may represent JSON null.
/// </summary>
/// <param name="ProducerSucceeded">Whether the producer step completed successfully.</param>
/// <param name="HasOutput">Whether a result was produced; true can accompany a serialized JSON null value.</param>
/// <param name="OutputJson">The serialized producer result, or null when no serialized output is available.</param>
public sealed record StoredStepOutput(
    bool ProducerSucceeded,
    bool HasOutput,
    string? OutputJson);

/// <summary>
/// Reads a producer's completion state and serialized output for argument binding.
/// </summary>
public interface IStepOutputReader
{
    /// <summary>
    /// Reads a step's producer success and output availability without invoking the step.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The stored output metadata, or null if the run or step does not exist.</returns>
    Task<StoredStepOutput?> ReadAsync(Guid runId, string jobId, string stepId, CancellationToken cancellationToken);
}