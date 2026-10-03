using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Contains the serialized business arguments for a step; the runtime supplies cancellation separately.
/// </summary>
/// <param name="ArgumentsJson">The JSON array of bound method arguments, excluding the runtime-supplied cancellation token.</param>
public sealed record BoundInvocation(
    string ArgumentsJson);

/// <summary>
/// Captures a command, producer, or poll result without conflating output presence with its value.
/// </summary>
/// <param name="HasOutput">Whether a result was produced; true can accompany a serialized JSON null value.</param>
/// <param name="OutputJson">The serialized producer result, or null when no serialized output is available.</param>
/// <param name="PollSatisfied">The Boolean check result for a poll, or null for a command or producer.</param>
public sealed record StepInvocationResult(
    bool HasOutput,
    string? OutputJson,
    bool? PollSatisfied);

/// <summary>
/// Resolves declared outputs and captured values into serializable service-method arguments.
/// </summary>
public interface IStepArgumentBinder
{
    /// <summary>
    /// Resolves successful producer outputs and evaluates supported expressions into a JSON array of business arguments.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="definition">The step descriptor selecting the service method and declared inputs.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The bound arguments, excluding the cancellation token.</returns>
    /// <exception cref="InvalidOperationException">The invocation is invalid or a required successful producer output is unavailable.</exception>
    Task<BoundInvocation> BindAsync(
        Guid runId,
        StepDefinition definition,
        CancellationToken cancellationToken);
}

/// <summary>
/// Invokes a configured service method with bound arguments and a logger scoped to its ownership claim.
/// </summary>
public interface IStepInvoker
{
    /// <summary>
    /// Invokes the selected service method in a new scope with bound arguments, cancellation, and claim-scoped logging.
    /// </summary>
    /// <param name="definition">The step descriptor selecting the service method and declared inputs.</param>
    /// <param name="invocation">The persisted JSON business arguments to deserialize for the selected method.</param>
    /// <param name="claim">The ownership identity used to scope and fence invocation logs.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The serialized producer output or Boolean polling result, as appropriate to the step kind.</returns>
    /// <remarks>Step services must be registered in dependency injection. The final method argument is cancellation. Exceptions from the service method propagate to the caller.</remarks>
    Task<StepInvocationResult> InvokeAsync(
        StepDefinition definition,
        BoundInvocation invocation,
        StepClaim claim,
        CancellationToken cancellationToken);
}