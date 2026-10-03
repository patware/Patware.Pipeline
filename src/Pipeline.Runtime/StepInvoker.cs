using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Resolves a step service in a new dependency injection scope and invokes it with persisted arguments.
/// </summary>
/// <param name="services">The service provider used to create a new scope for each step invocation.</param>
public sealed class StepInvoker(IServiceProvider services) : IStepInvoker
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
    public async Task<StepInvocationResult> InvokeAsync(StepDefinition definition, BoundInvocation invocation, StepClaim claim, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (definition.Invocation.Body is not MethodCallExpression call)
        {
            throw new InvalidOperationException(
                "The step invocation must be a method call.");
        }

        var methodParameters = call.Method.GetParameters();

        if (methodParameters.Length == 0 ||
            methodParameters[^1].ParameterType !=
                typeof(CancellationToken))
        {
            throw new InvalidOperationException(
                "The method must end with a CancellationToken parameter.");
        }

        using var document = JsonDocument.Parse(invocation.ArgumentsJson);

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array ||
            root.GetArrayLength() != methodParameters.Length - 1)
        {
            throw new InvalidOperationException(
                "Saved arguments do not match the selected method.");
        }

        var arguments = new object?[methodParameters.Length];

        for (var index = 0; index < arguments.Length - 1; index++)
        {
            arguments[index] = JsonSerializer.Deserialize(
                root[index].GetRawText(),
                methodParameters[index].ParameterType);
        }

        arguments[^1] = cancellationToken;

        await using var invocationScope = services.CreateAsyncScope();

        var stepLogger = invocationScope.ServiceProvider.GetRequiredService<PipelineStepLogger>();

        stepLogger.Initialize(claim);

        var service = invocationScope.ServiceProvider.GetRequiredService(definition.ServiceType);

        Task task;

        try
        {
            task = call.Method.Invoke(service, arguments) as Task
                ?? throw new InvalidOperationException(
                    "The selected method did not return a Task.");
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();

            throw;
        }

        await task;

        switch (definition.Kind)
        {
            case StepKind.Command:
                return new StepInvocationResult(
                    HasOutput: false,
                    OutputJson: null,
                    PollSatisfied: null);

            case StepKind.Poll:
                if (task is not Task<bool> pollTask)
                {
                    throw new InvalidOperationException(
                        "A poll must return Task<bool>.");
                }

                return new StepInvocationResult(
                    HasOutput: false,
                    OutputJson: null,
                    PollSatisfied: await pollTask);

            case StepKind.Produces:
                var outputType = definition.OutputType
                    ?? throw new InvalidOperationException(
                        "The producer has no output type.");

                var expectedTaskType = typeof(Task<>).MakeGenericType(outputType);

                if (!expectedTaskType.IsInstanceOfType(task))
                {
                    throw new InvalidOperationException(
                        "The task does not match the declared output type.");
                }

                var result = expectedTaskType
                    .GetProperty("Result")!
                    .GetValue(task);

                return new StepInvocationResult(
                    HasOutput: true,
                    OutputJson: JsonSerializer.Serialize(
                        result,
                        outputType),
                    PollSatisfied: null);

            default:
                throw new InvalidOperationException(
                    $"Unsupported step kind '{definition.Kind}'.");
        }
    }
}