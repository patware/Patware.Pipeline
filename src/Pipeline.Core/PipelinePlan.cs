using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Pipeline.Core;

/// <summary>
/// Describes an immutable pipeline graph; contains executable expressions rather than persisted execution state.
/// </summary>
/// <seealso cref="IPipelineBuilder" />
/// <seealso cref="PipelineRequest" />
/// <param name="Definition">The pipeline definition whose identity and version must match the executable plan.</param>
/// <param name="Jobs">The jobs in registration order; their dependencies determine execution eligibility.</param>
public sealed record PipelinePlan(
    PipelineDefinition Definition,
    ImmutableArray<JobDefinition> Jobs);

/// <summary>
/// Describes a job's prerequisites, optional condition, and steps in execution order.
/// </summary>
/// <seealso cref="IJobBuilder" />
/// <param name="Id">The unique job identifier within this plan.</param>
/// <param name="Dependencies">The predecessor jobs that must satisfy their completion requirements before this job starts.</param>
/// <param name="Condition">The optional job predicate; null means the job runs unconditionally.</param>
/// <param name="Steps">The steps in the order they must execute within this job.</param>
public sealed record JobDefinition(
    string Id,
    ImmutableArray<JobDependency> Dependencies,
    ConditionDefinition? Condition,
    ImmutableArray<StepDefinition> Steps);

/// <summary>
/// Describes the predecessor job and whether a condition-skipped predecessor satisfies the dependency.
/// </summary>
/// <param name="JobId">The job identifier within the plan or run.</param>
/// <param name="AllowConditionSkipped">Whether a predecessor skipped by its condition satisfies this dependency.</param>
public sealed record JobDependency(
    string JobId,
    bool AllowConditionSkipped);

/// <summary>
/// Identifies a producer step and the CLR type required when reading its persisted output.
/// </summary>
/// <param name="JobId">The job identifier within the plan or run.</param>
/// <param name="StepId">The step identifier within its job.</param>
/// <param name="ValueType">The CLR type used to deserialize the producer's output.</param>
public sealed record OutputReference(
    string JobId,
    string StepId,
    Type ValueType);

/// <summary>
/// Describes a job's Boolean predicate over a declared producer output.
/// </summary>
/// <param name="Source">The typed producer output supplied to the predicate.</param>
/// <param name="Predicate">The supported Boolean expression; method calls and user-defined operators are rejected.</param>
public sealed record ConditionDefinition(
    OutputReference Source,
    LambdaExpression Predicate);

/// <summary>
/// Distinguishes commands, output producers, and repeated Boolean checks.
/// </summary>
public enum StepKind
{
    /// <summary>
    /// Invokes a Task-returning method without publishing output.
    /// </summary>
    Command,
    /// <summary>
    /// Invokes a Task-returning producer and persists its typed result as JSON.
    /// </summary>
    Produces,
    /// <summary>
    /// Repeats a Task-returning Boolean check until true or timeout.
    /// </summary>
    Poll
}

/// <summary>
/// Defines the interval between unsuccessful checks and the total polling timeout.
/// </summary>
/// <param name="Every">The positive interval between unsuccessful polling checks.</param>
/// <param name="Timeout">The positive total polling timeout, measured from the first attempt.</param>
public sealed record PollSettings(
    TimeSpan Every,
    TimeSpan Timeout);

/// <summary>
/// Describes a service invocation, its declared input outputs, and its execution mode.
/// </summary>
/// <seealso cref="IStepBuilder{TStep}" />
/// <seealso cref="IPollBuilder{TStep}" />
/// <param name="Id">The unique step identifier within its job.</param>
/// <param name="Kind">The mode that determines how this step's invocation result is interpreted.</param>
/// <param name="ServiceType">The step service type resolved from dependency injection for each invocation.</param>
/// <param name="Invocation">The direct service-method expression with service, bound inputs, and cancellation parameters.</param>
/// <param name="Inputs">The producer outputs bound to the invocation's input parameters in declaration order.</param>
/// <param name="OutputType">The declared result type for a producer; null for commands and polls.</param>
/// <param name="Poll">The scheduling settings for a polling step; null for other step kinds.</param>
public sealed record StepDefinition(
    string Id,
    StepKind Kind,
    Type ServiceType,
    LambdaExpression Invocation,
    ImmutableArray<OutputReference> Inputs,
    Type? OutputType,
    PollSettings? Poll);