using System.Linq.Expressions;
using System.Reflection;

namespace Pipeline.Core;

/// <summary>
/// Validates the expression subset that can be bound to persisted step arguments and job conditions.
/// </summary>
public static class ArgumentExpressionValidator
{
    /// <summary>
    /// Validates business arguments using declared outputs, constants, captured instance fields or properties, and built-in conversions.
    /// </summary>
    /// <param name="invocation">The lambda whose first parameter is the service, last is cancellation, and intermediate parameters are declared outputs.</param>
    /// <param name="call">The method-call body whose business arguments are validated; the final cancellation argument is excluded.</param>
    /// <exception cref="InvalidOperationException">The expression uses unsupported nodes, static members, indexers, or user-defined operators.</exception>
    public static void ValidateInvocation(LambdaExpression invocation, MethodCallExpression call)
    {
        // First parameter: service.
        // Last parameter: cancellation token.
        // Parameters between them: declared output inputs.
        var inputs = invocation.Parameters
            .Skip(1)
            .Take(invocation.Parameters.Count - 2)
            .ToHashSet();

        // The final method argument is the cancellation token.
        foreach (var argument in call.Arguments.Take(
            call.Arguments.Count - 1))
        {
            ValidateValue(argument, inputs);
        }
    }

    /// <summary>
    /// Validates a condition using supported values, built-in comparisons, and Boolean negation, conjunction, or disjunction.
    /// </summary>
    /// <param name="condition">The Boolean expression to validate.</param>
    /// <exception cref="InvalidOperationException">The expression uses unsupported nodes, static members, indexers, or user-defined operators.</exception>
    public static void ValidateCondition(
        LambdaExpression condition)
    {
        var inputs = condition.Parameters.ToHashSet();

        ValidatePredicate(condition.Body, inputs);
    }

    private static void ValidatePredicate(Expression expression, HashSet<ParameterExpression> inputs)
    {
        if (expression is UnaryExpression
            {
                NodeType: ExpressionType.Not,
                Method: null
            } not)
        {
            ValidatePredicate(not.Operand, inputs);
            return;
        }

        if (expression is BinaryExpression binary)
        {
            if (binary.NodeType is
                ExpressionType.AndAlso or
                ExpressionType.OrElse)
            {
                if (binary.Method is not null)
                {
                    throw Unsupported(expression);
                }

                ValidatePredicate(binary.Left, inputs);
                ValidatePredicate(binary.Right, inputs);
                return;
            }

            if (binary.NodeType is
                ExpressionType.Equal or
                ExpressionType.NotEqual or
                ExpressionType.GreaterThan or
                ExpressionType.GreaterThanOrEqual or
                ExpressionType.LessThan or
                ExpressionType.LessThanOrEqual)
            {
                // Initially allow built-in comparisons only.
                if (binary.Method is not null)
                {
                    throw Unsupported(expression);
                }

                ValidateValue(binary.Left, inputs);
                ValidateValue(binary.Right, inputs);
                return;
            }
        }

        ValidateValue(expression, inputs);
    }

    private static void ValidateValue(
        Expression expression,
        HashSet<ParameterExpression> inputs)
    {
        switch (expression)
        {
            case ConstantExpression:
                return;

            case ParameterExpression parameter
                when inputs.Contains(parameter):
                return;

            case MemberExpression member
                when member.Expression is not null:
                ValidateMember(member);
                ValidateValue(member.Expression, inputs);
                return;

            case UnaryExpression conversion
                when conversion.NodeType is
                    ExpressionType.Convert or
                    ExpressionType.ConvertChecked
                    && conversion.Method is null:
                ValidateValue(conversion.Operand, inputs);
                return;

            default:
                throw Unsupported(expression);
        }
    }

    private static void ValidateMember(MemberExpression member)
    {
        if (member.Member is FieldInfo field && !field.IsStatic)
        {
            return;
        }

        if (member.Member is PropertyInfo property &&
            property.GetMethod is { IsStatic: false } &&
            property.GetIndexParameters().Length == 0)
        {
            return;
        }

        throw Unsupported(member);
    }

    private static InvalidOperationException Unsupported(
        Expression expression)
    {
        return new InvalidOperationException(
            $"Expression '{expression}' is not supported. " +
            "Use captured data, declared output values, " +
            "or ordinary instance properties and fields.");
    }
}