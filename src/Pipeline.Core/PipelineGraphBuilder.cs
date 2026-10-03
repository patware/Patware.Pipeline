using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Pipeline.Core;

internal sealed class PipelineGraphBuilder(PipelineDefinition definition) : IPipelineBuilder
{
    private readonly Guid _builderId = Guid.NewGuid();

    private readonly List<JobBuilder> _jobs = [];

    private bool _built;

    public IJobBuilder AddJob(string jobId)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        if (_jobs.Any(job => job.Id == jobId))
        {
            throw new InvalidOperationException(
                $"Job '{jobId}' already exists.");
        }

        var job = new JobBuilder(this, jobId);
        _jobs.Add(job);

        return job;
    }

    public PipelinePlan Build()
    {
        EnsureMutable();

        if (_jobs.Count == 0)
        {
            throw new InvalidOperationException(
                "The pipeline must contain at least one job.");
        }

        // Detect cycles before traversing ancestry for output validation.
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        foreach (var job in _jobs)
        {
            Visit(job, visiting, visited);
        }

        foreach (var job in _jobs)
        {
            if (job.Steps.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Job '{job.Id}' must contain at least one step.");
            }

            if (job.Condition is not null)
            {
                ValidateOutput(
                    job.Condition.Source,
                    job,
                    consumerIndex: -1);
            }

            for (var index = 0; index < job.Steps.Count; index++)
            {
                var registration = job.Steps[index];

                var step = registration.Definition
                    ?? throw new InvalidOperationException(
                        $"Step '{job.Id}/{registration.Id}' is incomplete.");

                foreach (var input in step.Inputs)
                {
                    ValidateOutput(input, job, index);
                }
            }
        }

        var plan = new PipelinePlan(
            definition,
            _jobs.Select(job => new JobDefinition(
                job.Id,
                job.Dependencies.ToImmutableArray(),
                job.Condition,
                job.Steps
                    .Select(step => step.Definition!)
                    .ToImmutableArray()))
                .ToImmutableArray());

        _built = true;

        return plan;
    }

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "This pipeline has already been built.");
        }
    }

    private OutputReference Reference<T>(StepOutput<T> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.BuilderId != _builderId)
        {
            throw new InvalidOperationException(
                "The output belongs to another pipeline builder.");
        }

        return new OutputReference(
            output.JobId,
            output.StepId,
            typeof(T));
    }

    private JobBuilder FindJob(string id)
    {
        return _jobs.Single(job => job.Id == id);
    }

    private void Visit(
        JobBuilder job,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(job.Id))
        {
            return;
        }

        if (!visiting.Add(job.Id))
        {
            throw new InvalidOperationException(
                $"A dependency cycle includes job '{job.Id}'.");
        }

        foreach (var dependency in job.Dependencies)
        {
            Visit(FindJob(dependency.JobId), visiting, visited);
        }

        visiting.Remove(job.Id);
        visited.Add(job.Id);
    }

    private bool HasAncestor(JobBuilder job, string ancestorId)
    {
        return job.Dependencies.Any(dependency =>
            dependency.JobId == ancestorId ||
            HasAncestor(FindJob(dependency.JobId), ancestorId));
    }

    private void ValidateOutput(
        OutputReference output,
        JobBuilder consumer,
        int consumerIndex)
    {
        var producerJob = FindJob(output.JobId);

        var producerIndex = producerJob.Steps.FindIndex(
            step => step.Id == output.StepId);

        if (producerIndex < 0)
        {
            throw new InvalidOperationException(
                $"Output producer '{output.JobId}/{output.StepId}' " +
                "does not exist.");
        }

        var producer = producerJob.Steps[producerIndex].Definition;

        if (producer is null ||
            producer.Kind != StepKind.Produces ||
            producer.OutputType != output.ValueType)
        {
            throw new InvalidOperationException(
                $"Step '{output.JobId}/{output.StepId}' " +
                "does not publish the required output type.");
        }

        if (producerJob == consumer)
        {
            if (producerIndex >= consumerIndex)
            {
                throw new InvalidOperationException(
                    $"Job '{consumer.Id}' consumes an output before " +
                    "its producer can finish.");
            }

            return;
        }

        if (!HasAncestor(consumer, producerJob.Id))
        {
            throw new InvalidOperationException(
                $"Job '{consumer.Id}' must depend on " +
                $"producer job '{producerJob.Id}'.");
        }
    }

    private sealed class JobBuilder(
        PipelineGraphBuilder owner,
        string id) : IJobBuilder
    {
        public PipelineGraphBuilder Owner { get; } = owner;

        public string Id { get; } = id;

        public List<JobDependency> Dependencies { get; } = [];

        public List<StepRegistration> Steps { get; } = [];

        public ConditionDefinition? Condition { get; private set; }

        public IJobBuilder After(
            IJobBuilder predecessor,
            bool allowConditionSkipped = false)
        {
            Owner.EnsureMutable();

            if (predecessor is not JobBuilder other ||
                other.Owner != Owner)
            {
                throw new InvalidOperationException(
                    "The predecessor belongs to another pipeline.");
            }

            if (Dependencies.Any(dependency =>
                dependency.JobId == other.Id))
            {
                throw new InvalidOperationException(
                    $"Dependency on '{other.Id}' is already registered.");
            }

            Dependencies.Add(new JobDependency(
                other.Id,
                allowConditionSkipped));

            return this;
        }

        public IJobBuilder When<TOutput>(
            StepOutput<TOutput> source,
            Expression<Func<TOutput, bool>> predicate)
        {
            Owner.EnsureMutable();
            ArgumentNullException.ThrowIfNull(predicate);

            if (Condition is not null)
            {
                throw new InvalidOperationException(
                    $"Job '{Id}' already has a condition.");
            }

            ArgumentExpressionValidator.ValidateCondition(predicate);

            Condition = new ConditionDefinition(
                Owner.Reference(source),
                predicate);

            return this;
        }

        public IStepBuilder<TStep> Step<TStep>(string stepId)
            where TStep : class
        {
            return new StepBuilder<TStep>(Reserve(stepId));
        }

        public IPollBuilder<TStep> Poll<TStep>(string stepId)
            where TStep : class
        {
            return new PollBuilder<TStep>(Reserve(stepId));
        }

        private StepRegistration Reserve(string stepId)
        {
            Owner.EnsureMutable();
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

            if (Steps.Any(step => step.Id == stepId))
            {
                throw new InvalidOperationException(
                    $"Step '{Id}/{stepId}' already exists.");
            }

            var registration = new StepRegistration(this, stepId);
            Steps.Add(registration);

            return registration;
        }
    }

    private sealed class StepRegistration(JobBuilder job, string id)
    {
        public string Id { get; } = id;

        public StepDefinition? Definition { get; private set; }

        private ImmutableArray<OutputReference> _inputs = [];

        private bool _bound;

        public void Bind(params OutputReference[] inputs)
        {
            EnsureOpen();

            if (_bound)
            {
                throw new InvalidOperationException(
                    $"Step '{job.Id}/{Id}' already has input bindings.");
            }

            _inputs = inputs.ToImmutableArray();
            _bound = true;
        }

        public OutputReference Reference<T>(StepOutput<T> output)
        {
            return job.Owner.Reference(output);
        }

        public IJobBuilder Command<TStep>(LambdaExpression call)
        {
            Complete(
                typeof(TStep),
                StepKind.Command,
                call,
                outputType: null,
                poll: null);

            return job;
        }

        public StepOutput<TResult> Produce<TStep, TResult>(
            LambdaExpression call)
        {
            Complete(
                typeof(TStep),
                StepKind.Produces,
                call,
                typeof(TResult),
                poll: null);

            return new StepOutput<TResult>(
                job.Owner._builderId,
                job.Id,
                Id);
        }

        public IJobBuilder Poll<TStep>(
            LambdaExpression call,
            TimeSpan every,
            TimeSpan timeout)
        {
            if (every <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(every));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            Complete(
                typeof(TStep),
                StepKind.Poll,
                call,
                outputType: null,
                new PollSettings(every, timeout));

            return job;
        }

        private void Complete(
            Type serviceType,
            StepKind kind,
            LambdaExpression call,
            Type? outputType,
            PollSettings? poll)
        {
            EnsureOpen();
            ArgumentNullException.ThrowIfNull(call);

            if (call.Parameters.Count != _inputs.Length + 2 ||
                call.Parameters[0].Type != serviceType ||
                call.Parameters[^1].Type != typeof(CancellationToken))
            {
                throw new InvalidOperationException(
                    "The invocation parameters do not match " +
                    "the service, inputs, and cancellation token.");
            }

            for (var index = 0; index < _inputs.Length; index++)
            {
                if (call.Parameters[index + 1].Type !=
                    _inputs[index].ValueType)
                {
                    throw new InvalidOperationException(
                        "An invocation input has the wrong type.");
                }
            }

            if (call.Body is not MethodCallExpression methodCall ||
                methodCall.Object != call.Parameters[0])
            {
                throw new InvalidOperationException(
                    "The invocation must directly call an instance " +
                    "method on the step parameter.");
            }

            var expectedReturnType = kind switch
            {
                StepKind.Command => typeof(Task),
                StepKind.Produces =>
                    typeof(Task<>).MakeGenericType(outputType!),
                StepKind.Poll => typeof(Task<bool>),
                _ => throw new InvalidOperationException(
                    "Unsupported step kind.")
            };

            if (methodCall.Method.ReturnType != expectedReturnType)
            {
                throw new InvalidOperationException(
                    $"The selected method must return " +
                    $"'{expectedReturnType}'.");
            }

            var parameters = methodCall.Method.GetParameters();

            if (parameters.Length == 0 ||
                parameters[^1].ParameterType !=
                    typeof(CancellationToken) ||
                methodCall.Arguments[^1] != call.Parameters[^1])
            {
                throw new InvalidOperationException(
                    "The selected method must receive the supplied " +
                    "cancellation token as its final argument.");
            }

            if (parameters.Any(parameter =>
                parameter.ParameterType.IsByRef))
            {
                throw new InvalidOperationException(
                    "ref and out arguments are not supported.");
            }

            ArgumentExpressionValidator.ValidateInvocation(call, methodCall);

            Definition = new StepDefinition(
                Id,
                kind,
                serviceType,
                call,
                _inputs,
                outputType,
                poll);
        }

        private void EnsureOpen()
        {
            job.Owner.EnsureMutable();

            if (Definition is not null)
            {
                throw new InvalidOperationException(
                    $"Step '{job.Id}/{Id}' is already complete.");
            }
        }
    }

    private sealed class StepBuilder<TStep>(
        StepRegistration registration) : IStepBuilder<TStep>
        where TStep : class
    {
        public IJobBuilder Execute(
            Expression<Func<TStep, CancellationToken, Task>> call)
        {
            return registration.Command<TStep>(call);
        }

        public StepOutput<TResult> Produces<TResult>(
            Expression<Func<TStep, CancellationToken, Task<TResult>>> call)
        {
            return registration.Produce<TStep, TResult>(call);
        }

        public IBoundStepBuilder<TStep, TInput> Using<TInput>(
            StepOutput<TInput> input)
        {
            registration.Bind(registration.Reference(input));

            return new BoundStepBuilder<TStep, TInput>(registration);
        }

        public IBoundStepBuilder<TStep, TFirst, TSecond>
            Using<TFirst, TSecond>(
                StepOutput<TFirst> first,
                StepOutput<TSecond> second)
        {
            registration.Bind(
                registration.Reference(first),
                registration.Reference(second));

            return new BoundStepBuilder<TStep, TFirst, TSecond>(
                registration);
        }
    }

    private sealed class BoundStepBuilder<TStep, TInput>(
        StepRegistration registration)
        : IBoundStepBuilder<TStep, TInput>
        where TStep : class
    {
        public IJobBuilder Execute(
            Expression<
                Func<TStep, TInput, CancellationToken, Task>> call)
        {
            return registration.Command<TStep>(call);
        }

        public StepOutput<TResult> Produces<TResult>(
            Expression<
                Func<TStep, TInput, CancellationToken, Task<TResult>>> call)
        {
            return registration.Produce<TStep, TResult>(call);
        }
    }

    private sealed class BoundStepBuilder<TStep, TFirst, TSecond>(
        StepRegistration registration)
        : IBoundStepBuilder<TStep, TFirst, TSecond>
        where TStep : class
    {
        public IJobBuilder Execute(
            Expression<
                Func<TStep, TFirst, TSecond, CancellationToken, Task>> call)
        {
            return registration.Command<TStep>(call);
        }

        public StepOutput<TResult> Produces<TResult>(
            Expression<
                Func<
                    TStep,
                    TFirst,
                    TSecond,
                    CancellationToken,
                    Task<TResult>>> call)
        {
            return registration.Produce<TStep, TResult>(call);
        }
    }

    private sealed class PollBuilder<TStep>(
        StepRegistration registration) : IPollBuilder<TStep>
        where TStep : class
    {
        public IJobBuilder Check(
            Expression<Func<TStep, CancellationToken, Task<bool>>> call,
            TimeSpan every,
            TimeSpan timeout)
        {
            return registration.Poll<TStep>(call, every, timeout);
        }

        public IBoundPollBuilder<TStep, TInput> Using<TInput>(
            StepOutput<TInput> input)
        {
            registration.Bind(registration.Reference(input));

            return new BoundPollBuilder<TStep, TInput>(registration);
        }
    }

    private sealed class BoundPollBuilder<TStep, TInput>(
        StepRegistration registration)
        : IBoundPollBuilder<TStep, TInput>
        where TStep : class
    {
        public IJobBuilder Check(
            Expression<
                Func<TStep, TInput, CancellationToken, Task<bool>>> call,
            TimeSpan every,
            TimeSpan timeout)
        {
            return registration.Poll<TStep>(call, every, timeout);
        }
    }
}