using Microsoft.Extensions.DependencyInjection;
using Pipeline.Core;
using Pipeline.Tests.Shared;

namespace Pipeline.Runtime.Tests;

[TestClass]
public class ExecutionTests
{
    private ServiceProvider provider = null!;
    private IServiceScope scope = null!;
    private readonly TestClock clock = new();
    private readonly ITestStep step = Substitute.For<ITestStep>();
    private PipelineExecutionCoordinator Coordinator => scope.ServiceProvider.GetRequiredService<PipelineExecutionCoordinator>();
    private IPipelineRuntime Runtime => provider.GetRequiredService<IPipelineRuntime>();
    private IPipelineExecutionStore Store => provider.GetRequiredService<IPipelineExecutionStore>();

    [TestInitialize]
    public void Initialize()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton(step);
        services.AddPipeline();
        var registry = Substitute.For<IPipelineDefinitionRegistry>();
        registry.Restore(Arg.Any<PipelineExecutionSpecification>()).Returns(_ => plan!);
        services.AddSingleton(registry);
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        scope = provider.CreateScope();
    }

    private PipelinePlan? plan;
    private async Task<PipelineRun> Enqueue(IPipelineBuilder builder)
    {
        plan = builder.Build();
        return await Runtime.EnqueueAsync(Samples.Request(plan));
    }
    private static IPipelineBuilder Builder() => new PipelineBuilderFactory().Create(new("execution", "Execution"));
    private async Task<PipelineExecutionSnapshot> Drain(Guid id)
    {
        // A bound catches accidental nontermination without wall-clock sleeps.
        for (var i = 0; i < 30; i++)
            if (!await Coordinator.AdvanceAsync(id, default)) return (await Store.LoadAsync(id, default))!;
        Assert.Fail("The coordinator did not become idle within 30 transitions.");
        return null!;
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        scope.Dispose();
        await provider.DisposeAsync();
    }

    [TestMethod]
    public async Task Producer_output_binds_to_dependent_command_and_run_completes()
    {
        step.Produce("hello", Arg.Any<CancellationToken>()).Returns("world");
        var builder = Builder();
        var producer = builder.AddJob("producer");
        var output = producer.Step<ITestStep>("produce").Produces((s, ct) => s.Produce("hello", ct));
        builder.AddJob("consumer").After(producer).Step<ITestStep>("consume").Using(output).Execute((s, value, ct) => s.Execute(value, ct));
        var run = await Enqueue(builder);
        run.CreatedAt.Should().Be(clock.Now);
        run.Status.Should().Be(PipelineStatus.Queued);
        var result = await Drain(run.Id);
        result.Run.Status.Should().Be(PipelineStatus.Completed);
        result.Steps.Should().OnlyContain(x => x.Status == StepExecutionStatus.Succeeded && x.Attempt == 1);
        result.Steps.Single(x => x.StepId == "produce").OutputJson.Should().Be("\"world\"");
        await step.Received(1).Execute("world", Arg.Any<CancellationToken>());
        (await Runtime.GetExecutionAsync(run.Id))!.Jobs.Select(x => x.JobId).Should().Equal("producer", "consumer");
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task False_condition_skips_job_and_dependency_policy_controls_downstream(bool allowSkipped)
    {
        step.Produce("hello", Arg.Any<CancellationToken>()).Returns("");
        var builder = Builder();
        var producer = builder.AddJob("producer");
        var output = producer.Step<ITestStep>("p").Produces((s, ct) => s.Produce("hello", ct));
        var conditional = builder.AddJob("conditional").After(producer).When(output, x => x.Length > 0);
        conditional.Step<ITestStep>("skip").Execute((s, ct) => s.Execute("skipped", ct));
        builder.AddJob("last").After(conditional, allowSkipped).Step<ITestStep>("last").Execute((s, ct) => s.Execute("last", ct));
        var run = await Enqueue(builder);
        var result = await Drain(run.Id);
        result.Run.Status.Should().Be(allowSkipped ? PipelineStatus.Completed : PipelineStatus.Failed);
        result.Jobs.Single(x => x.JobId == "conditional").Status.Should().Be(JobExecutionStatus.ConditionSkipped);
        result.Jobs.Single(x => x.JobId == "last").Status.Should().Be(allowSkipped ? JobExecutionStatus.Succeeded : JobExecutionStatus.Blocked);
        await step.DidNotReceive().Execute("skipped", Arg.Any<CancellationToken>());
        await step.Received(allowSkipped ? 1 : 0).Execute("last", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Failure_is_recorded_and_retry_preserves_successful_steps_and_arguments()
    {
        step.Produce("hello", Arg.Any<CancellationToken>()).Returns("result");
        step.Execute("result", Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("service unavailable")), Task.CompletedTask);
        var builder = Builder();
        var job = builder.AddJob("job");
        var output = job.Step<ITestStep>("produce").Produces((s, ct) => s.Produce("hello", ct));
        job.Step<ITestStep>("execute").Using(output).Execute((s, x, ct) => s.Execute(x, ct));
        var run = await Enqueue(builder);
        var failed = await Drain(run.Id);
        failed.Run.Status.Should().Be(PipelineStatus.Failed);
        failed.Steps[1].Error.Should().Be("service unavailable");
        (await Runtime.RetryAsync(run.Id)).Should().BeTrue();
        (await Runtime.RetryAsync(run.Id)).Should().BeFalse();
        var complete = await Drain(run.Id);
        complete.Run.Status.Should().Be(PipelineStatus.Completed);
        complete.Steps[1].Attempt.Should().Be(2);
        complete.Steps[1].ArgumentsJson.Should().Be(failed.Steps[1].ArgumentsJson);
        await step.Received(1).Produce("hello", Arg.Any<CancellationToken>());
        await step.Received(2).Execute("result", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task Poll_waits_until_due_then_succeeds_or_times_out(bool timeout)
    {
        step.Poll(Arg.Any<CancellationToken>()).Returns(false, true);
        var builder = Builder();
        builder.AddJob("job").Poll<ITestStep>("poll").Check((s, ct) => s.Poll(ct), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        var run = await Enqueue(builder);
        var waiting = await Drain(run.Id);
        waiting.Steps[0].Status.Should().Be(StepExecutionStatus.Waiting);
        waiting.Steps[0].NextAttemptAt.Should().Be(clock.Now.AddSeconds(5));
        waiting.Steps[0].PollDeadline.Should().Be(clock.Now.AddSeconds(10));
        (await Coordinator.AdvanceAsync(run.Id, default)).Should().BeFalse();
        clock.Now = clock.Now.AddSeconds(timeout ? 10 : 5);
        var result = await Drain(run.Id);
        result.Run.Status.Should().Be(timeout ? PipelineStatus.Failed : PipelineStatus.Completed);
        result.Steps[0].Status.Should().Be(timeout ? StepExecutionStatus.TimedOut : StepExecutionStatus.Succeeded);
        await step.Received(timeout ? 1 : 2).Poll(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Prepared_step_does_not_execute_and_stale_dispatch_is_ignored()
    {
        step.Produce("hello", Arg.Any<CancellationToken>()).Returns("done");
        plan = Samples.Plan();
        var run = await Runtime.EnqueueAsync(Samples.Request(plan));
        PipelineStepWorkItem? work = null;
        for (var i = 0; i < 5 && work is null; i++) work = (await Coordinator.PrepareNextStepAsync(run.Id, default)).Step;
        work.Should().NotBeNull();
        await step.DidNotReceive().Produce(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await Coordinator.ExecuteStepAsync(work! with { ExpectedAttempt = 2 }, default)).Should().BeFalse();
        (await Coordinator.ExecuteStepAsync(work!, default)).Should().BeTrue();
        (await Coordinator.ExecuteStepAsync(work!, default)).Should().BeFalse();
        await step.Received(1).Produce("hello", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Missing_runs_are_noops_for_execution_queries_and_retry()
    {
        var id = Guid.NewGuid();
        (await Coordinator.AdvanceAsync(id, default)).Should().BeFalse();
        (await Runtime.GetRunAsync(id)).Should().BeNull();
        (await Runtime.GetExecutionAsync(id)).Should().BeNull();
        (await Runtime.RetryAsync(id)).Should().BeFalse();
        (await Runtime.GetRunsAsync()).Should().BeEmpty();
    }
}
