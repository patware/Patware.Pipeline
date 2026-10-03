using System.Linq.Expressions;
using System.Text.Json;

using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Evaluates supported argument expressions against stored producer outputs and serializes the method arguments.
/// </summary>
/// <param name="outputReader">The reader supplying persisted producer results.</param>
public sealed class StepArgumentBinder(IStepOutputReader outputReader) : IStepArgumentBinder
{
    /// <summary>
    /// Resolves successful producer outputs and evaluates supported expressions into a JSON array of business arguments.
    /// </summary>
    /// <param name="runId">The identifier of the run to operate on.</param>
    /// <param name="definition">The step descriptor selecting the service method and declared inputs.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The bound arguments, excluding the cancellation token.</returns>
    /// <exception cref="InvalidOperationException">The invocation is invalid or a required successful producer output is unavailable.</exception>
    public async Task<BoundInvocation> BindAsync(
        Guid runId,
        StepDefinition definition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var invocation = definition.Invocation;

        if (invocation.Body is not MethodCallExpression call)
        {
            throw new InvalidOperationException(
                "The step invocation must be a method call.");
        }

        if (invocation.Parameters.Count != definition.Inputs.Length + 2)
        {
            throw new InvalidOperationException(
                "The invocation does not match its declared inputs.");
        }

        ArgumentExpressionValidator.ValidateInvocation(invocation, call);

        var values = new object?[definition.Inputs.Length];

        for (var index = 0; index < definition.Inputs.Length; index++)
        {
            var input = definition.Inputs[index];

            var stored = await outputReader.ReadAsync(
                runId,
                input.JobId,
                input.StepId,
                cancellationToken);

            if (stored is null ||
                !stored.ProducerSucceeded ||
                !stored.HasOutput ||
                stored.OutputJson is null)
            {
                throw new InvalidOperationException(
                    $"Output '{input.JobId}/{input.StepId}' " +
                    "is not available.");
            }

            values[index] = JsonSerializer.Deserialize(
                stored.OutputJson,
                input.ValueType);
        }

        var valuesParameter = Expression.Parameter(
            typeof(object[]),
            "values");

        var replacements =
            new Dictionary<ParameterExpression, Expression>();

        for (var index = 0; index < definition.Inputs.Length; index++)
        {
            var inputParameter = invocation.Parameters[index + 1];

            replacements.Add(
                inputParameter,
                Expression.Convert(
                    Expression.ArrayIndex(
                        valuesParameter,
                        Expression.Constant(index)),
                    inputParameter.Type));
        }

        var visitor = new InputSubstitutionVisitor(replacements);
        var methodParameters = call.Method.GetParameters();

        // The final parameter is supplied by the runtime.
        var businessArgumentCount = methodParameters.Length - 1;

        var serializedArguments =
            new JsonElement[businessArgumentCount];

        for (var index = 0; index < businessArgumentCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expression = visitor.Visit(call.Arguments[index])
                ?? throw new InvalidOperationException(
                    "An argument expression could not be resolved.");

            var evaluate =
                Expression.Lambda<Func<object?[], object?>>(
                    Expression.Convert(expression, typeof(object)),
                    valuesParameter)
                .Compile();

            var value = evaluate(values);

            serializedArguments[index] =
                JsonSerializer.SerializeToElement(
                    value,
                    methodParameters[index].ParameterType);
        }

        return new BoundInvocation(
            JsonSerializer.Serialize(serializedArguments));
    }

    private sealed class InputSubstitutionVisitor(IReadOnlyDictionary<ParameterExpression, Expression> replacements) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return replacements.TryGetValue(node, out var replacement)
                ? replacement
                : base.VisitParameter(node);
        }
    }
}