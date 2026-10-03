using System.Linq.Expressions;

namespace Pipeline.Core;

/// <summary>
/// Creates independent builders for versioned pipeline definitions.
/// </summary>
public interface IPipelineBuilderFactory
{
    /// <summary>
    /// Creates a builder for the supplied definition.
    /// </summary>
    /// <param name="definition">The versioned pipeline definition associated with this plan or run.</param>
    /// <returns>A new, independently mutable pipeline builder.</returns>
    IPipelineBuilder Create(PipelineDefinition definition);
}

/// <summary>
/// Builds an immutable execution graph of jobs, dependencies, conditions, and ordered steps.
/// </summary>
/// <remarks>
/// Jobs are connected with <see cref="IJobBuilder.After" />; steps within a job run in registration order.
/// Output references must belong to this builder and come from an earlier step or an ancestor job.
/// Use <see cref="PipelineRequest.Create{TInput, TSettings}" /> to package the completed plan for submission.
/// </remarks>
/// <example>
/// <code>
/// var definition = new PipelineDefinition("uppercase", "Uppercase sample");
/// var builder = new PipelineBuilderFactory().Create(definition);
/// var producer = builder.AddJob("produce");
/// var output = producer.Step&lt;ExampleStep&gt;("first")
///     .Produces((step, ct) =&gt; step.ExecuteAsync("hello", ct));
/// builder.AddJob("consume").After(producer)
///     .Step&lt;ExampleStep&gt;("second").Using(output)
///     .Produces((step, value, ct) =&gt; step.ExecuteAsync(value, ct));
/// var plan = builder.Build();
/// </code>
/// </example>
public interface IPipelineBuilder
{
    /// <summary>
    /// Adds a job with a unique, nonblank identifier to the graph.
    /// </summary>
    /// <param name="jobId">The job identifier within the plan or run.</param>
    /// <returns>The builder used to configure the new job.</returns>
    IJobBuilder AddJob(string jobId);

    /// <summary>
    /// Validates and freezes the graph, rejecting cycles, empty jobs, incomplete steps, and unavailable output dependencies.
    /// </summary>
    /// <returns>The immutable plan. The builder cannot be changed or built again afterward.</returns>
    /// <exception cref="InvalidOperationException">The graph is invalid or the builder has already been built.</exception>
    PipelinePlan Build();
}

/// <summary>
/// Configures one job's prerequisites, optional condition, and sequential steps.
/// </summary>
public interface IJobBuilder
{
    /// <summary>
    /// Gets the job identifier, unique within this builder's pipeline graph.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Requires a predecessor from the same builder to finish before this job can start.
    /// </summary>
    /// <param name="predecessor">A predecessor job from the same pipeline builder.</param>
    /// <param name="allowConditionSkipped">Whether a predecessor skipped by its condition satisfies this dependency.</param>
    /// <returns>This job builder for further configuration.</returns>
    IJobBuilder After(
        IJobBuilder predecessor,
        bool allowConditionSkipped = false);

    /// <summary>
    /// Sets the job's single condition against an output from an ancestor job; a false result skips the job.
    /// </summary>
    /// <typeparam name="TOutput">The producer result type.</typeparam>
    /// <param name="source">The typed producer output supplied to the predicate.</param>
    /// <param name="predicate">The supported Boolean expression; method calls and user-defined operators are rejected.</param>
    /// <returns>This job builder for further configuration.</returns>
    IJobBuilder When<TOutput>(
        StepOutput<TOutput> source,
        Expression<Func<TOutput, bool>> predicate);

    /// <summary>
    /// Reserves a uniquely identified step in this job's execution order.
    /// </summary>
    /// <typeparam name="TStep">The step service type resolved from dependency injection.</typeparam>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <returns>A builder used to select the step method and optional input bindings.</returns>
    IStepBuilder<TStep> Step<TStep>(string stepId)
        where TStep : class;

    /// <summary>
    /// Reserves a uniquely identified polling step in this job's execution order.
    /// </summary>
    /// <typeparam name="TStep">The step service type resolved from dependency injection.</typeparam>
    /// <param name="stepId">The step identifier within its job.</param>
    /// <returns>A builder used to select the Boolean check and its schedule.</returns>
    IPollBuilder<TStep> Poll<TStep>(string stepId)
        where TStep : class;
}

/// <summary>
/// Selects a service method for a command or output-producing step, optionally binding earlier outputs.
/// </summary>
/// <typeparam name="TStep">The step service type resolved from dependency injection.</typeparam>
public interface IStepBuilder<TStep>
    where TStep : class
{
    /// <summary>
    /// Completes the step as an asynchronous command that does not publish an output.
    /// </summary>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <returns>The containing job builder, allowing subsequent steps to be added.</returns>
    IJobBuilder Execute(
        Expression<Func<TStep, CancellationToken, Task>> call);

    /// <summary>
    /// Completes the step as an asynchronous producer whose result can be bound by later steps or dependent jobs.
    /// </summary>
    /// <typeparam name="TResult">The producer result type.</typeparam>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <returns>A typed output reference belonging to this builder, rather than an executed result.</returns>
    StepOutput<TResult> Produces<TResult>(
        Expression<Func<TStep, CancellationToken, Task<TResult>>> call);

    /// <summary>
    /// Binds producer outputs to the invocation parameters in the order supplied.
    /// </summary>
    /// <typeparam name="TInput">The input value type.</typeparam>
    /// <param name="input">The producer output supplied to the invocation input parameter.</param>
    /// <returns>A bound builder. Producers must occur earlier in this job or in an ancestor job.</returns>
    IBoundStepBuilder<TStep, TInput> Using<TInput>(
        StepOutput<TInput> input);

    /// <summary>
    /// Binds producer outputs to the invocation parameters in the order supplied.
    /// </summary>
    /// <typeparam name="TFirst">The first bound input type.</typeparam>
    /// <typeparam name="TSecond">The second bound input type.</typeparam>
    /// <param name="first">The producer output supplied as the first input parameter.</param>
    /// <param name="second">The producer output supplied as the second input parameter.</param>
    /// <returns>A bound builder. Producers must occur earlier in this job or in an ancestor job.</returns>
    IBoundStepBuilder<TStep, TFirst, TSecond> Using<TFirst, TSecond>(
        StepOutput<TFirst> first,
        StepOutput<TSecond> second);
}

/// <summary>
/// Selects a service method whose expression receives the previously bound outputs in binding order.
/// </summary>
/// <typeparam name="TStep">The step service type resolved from dependency injection.</typeparam>
/// <typeparam name="TInput">The input value type.</typeparam>
public interface IBoundStepBuilder<TStep, TInput>
    where TStep : class
{
    /// <summary>
    /// Completes the step as an asynchronous command that does not publish an output.
    /// </summary>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <returns>The containing job builder, allowing subsequent steps to be added.</returns>
    IJobBuilder Execute(
        Expression<Func<TStep, TInput, CancellationToken, Task>> call);

    /// <summary>
    /// Completes the step as an asynchronous producer whose result can be bound by later steps or dependent jobs.
    /// </summary>
    /// <typeparam name="TResult">The producer result type.</typeparam>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <returns>A typed output reference belonging to this builder, rather than an executed result.</returns>
    StepOutput<TResult> Produces<TResult>(
        Expression<
            Func<TStep, TInput, CancellationToken, Task<TResult>>> call);
}

/// <summary>
/// Selects a service method whose expression receives the previously bound outputs in binding order.
/// </summary>
/// <typeparam name="TStep">The step service type resolved from dependency injection.</typeparam>
/// <typeparam name="TFirst">The first bound input type.</typeparam>
/// <typeparam name="TSecond">The second bound input type.</typeparam>
public interface IBoundStepBuilder<TStep, TFirst, TSecond>
    where TStep : class
{
    /// <summary>
    /// Completes the step as an asynchronous command that does not publish an output.
    /// </summary>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <returns>The containing job builder, allowing subsequent steps to be added.</returns>
    IJobBuilder Execute(
        Expression<
            Func<TStep, TFirst, TSecond, CancellationToken, Task>> call);

    /// <summary>
    /// Completes the step as an asynchronous producer whose result can be bound by later steps or dependent jobs.
    /// </summary>
    /// <typeparam name="TResult">The producer result type.</typeparam>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <returns>A typed output reference belonging to this builder, rather than an executed result.</returns>
    StepOutput<TResult> Produces<TResult>(
        Expression<
            Func<
                TStep,
                TFirst,
                TSecond,
                CancellationToken,
                Task<TResult>>> call);
}

/// <summary>
/// Configures a Boolean service method to repeat until satisfied or its timeout expires.
/// </summary>
/// <typeparam name="TStep">The step service type resolved from dependency injection.</typeparam>
public interface IPollBuilder<TStep>
    where TStep : class
{
    /// <summary>
    /// Completes a polling step that succeeds on true and reschedules false results until its deadline.
    /// </summary>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <param name="every">The positive interval between unsuccessful polling checks.</param>
    /// <param name="timeout">The positive total polling timeout, measured from the first attempt.</param>
    /// <returns>The containing job builder for further configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The interval or timeout is not positive.</exception>
    IJobBuilder Check(
        Expression<Func<TStep, CancellationToken, Task<bool>>> call,
        TimeSpan every,
        TimeSpan timeout);

    /// <summary>
    /// Binds producer outputs to the invocation parameters in the order supplied.
    /// </summary>
    /// <typeparam name="TInput">The input value type.</typeparam>
    /// <param name="input">The producer output supplied to the invocation input parameter.</param>
    /// <returns>A bound builder. Producers must occur earlier in this job or in an ancestor job.</returns>
    IBoundPollBuilder<TStep, TInput> Using<TInput>(
        StepOutput<TInput> input);
}

/// <summary>
/// Configures a polling method that consumes an earlier step's output.
/// </summary>
/// <typeparam name="TStep">The step service type resolved from dependency injection.</typeparam>
/// <typeparam name="TInput">The input value type.</typeparam>
public interface IBoundPollBuilder<TStep, TInput>
    where TStep : class
{
    /// <summary>
    /// Completes a polling step that succeeds on true and reschedules false results until its deadline.
    /// </summary>
    /// <param name="call">A direct instance-method expression on the step service, with the supplied cancellation token as its final argument.</param>
    /// <param name="every">The positive interval between unsuccessful polling checks.</param>
    /// <param name="timeout">The positive total polling timeout, measured from the first attempt.</param>
    /// <returns>The containing job builder for further configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The interval or timeout is not positive.</exception>
    IJobBuilder Check(
        Expression<
            Func<TStep, TInput, CancellationToken, Task<bool>>> call,
        TimeSpan every,
        TimeSpan timeout);
}