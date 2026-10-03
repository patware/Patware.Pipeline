using Pipeline.Core;
using global::Hangfire;
using global::Hangfire.Common;
using global::Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Pipeline.Runtime;
using Pipeline.Tests.Shared;

namespace Pipeline.Hangfire.Tests;

[TestClass]
public class HangfireTests
{
    private readonly IBackgroundJobClient client = Substitute.For<IBackgroundJobClient>();
    private readonly IPipelineExecutionStore execution = Substitute.For<IPipelineExecutionStore>();
    private readonly IPipelineStore store = Substitute.For<IPipelineStore>();

    [TestMethod]
    public async Task Recovery_dispatches_only_runs_still_active_with_recovery_flag()
    {
        var active = Samples.Run();
        var complete = Samples.Run() with { Status = PipelineStatus.Completed };
        var missing = Guid.NewGuid();
        execution.ListActiveRunIdsAsync(default).Returns(new[] { active.Id, complete.Id, missing });
        store.GetAsync(active.Id).Returns(active);
        store.GetAsync(complete.Id).Returns(complete);
        await new PipelineRecoveryJob(execution, store, client).ExecuteAsync(default);
        client.Received(1).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineRunJob) && (Guid)j.Args[0] == active.Id && (string)j.Args[1] == active.Title && (bool)j.Args[2]), Arg.Any<EnqueuedState>());
        client.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [TestMethod]
    public async Task Startup_registers_minutely_recovery_and_immediate_discovery()
    {
        var recurring = Substitute.For<IRecurringJobManager>();
        var startup = new PipelineHangfireStartupService(recurring, client);
        await startup.StartAsync(default);
        recurring.Received(1).AddOrUpdate("pipeline-recovery", Arg.Is<Job>(j => j.Type == typeof(PipelineRecoveryJob)), "* * * * *", Arg.Any<RecurringJobOptions>());
        client.Received(1).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineRecoveryJob)), Arg.Any<EnqueuedState>());
        await startup.StopAsync(default);
    }

    [TestMethod]
    public async Task Cancelled_startup_does_not_dispatch()
    {
        var recurring = Substitute.For<IRecurringJobManager>();
        var startup = new PipelineHangfireStartupService(recurring, client);
        Func<Task> start = () => startup.StartAsync(new CancellationToken(true));
        await start.Should().ThrowAsync<OperationCanceledException>();
        client.ReceivedCalls().Should().BeEmpty();
        recurring.ReceivedCalls().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task Enqueue_persists_run_even_when_dispatch_fails(bool failDispatch)
    {
        using var provider = Services();
        if (failDispatch) client.Create(Arg.Any<Job>(), Arg.Any<IState>()).Returns(_ => throw new InvalidOperationException("offline"));
        var runtime = provider.GetRequiredService<IPipelineRuntime>();
        var run = await runtime.EnqueueAsync(Samples.Request());
        (await runtime.GetRunAsync(run.Id)).Should().BeEquivalentTo(run);
        (await runtime.GetRunsAsync()).Should().ContainSingle().Which.Id.Should().Be(run.Id);
        client.Received(1).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineRunJob) && (Guid)j.Args[0] == run.Id && !(bool)j.Args[2]), Arg.Any<EnqueuedState>());
        await runtime.Awaiting(x => x.RunAsync(default)).Should().ThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task Run_job_selects_step_and_step_job_requests_further_coordination()
    {
        using var provider = Services();
        var runtime = provider.GetRequiredService<IPipelineRuntime>();
        var run = await runtime.EnqueueAsync(Samples.Request());
        client.ClearReceivedCalls();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PipelineRunJob>().ExecuteAsync(run.Id, run.Title, false, default);
        client.Received(1).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineStepJob) && (string)j.Args[2] == "job" && (string)j.Args[3] == "step" && (int)j.Args[4] == 1), Arg.Any<EnqueuedState>());
        client.ClearReceivedCalls();
        await scope.ServiceProvider.GetRequiredService<PipelineStepJob>().ExecuteAsync(run.Id, run.Title, "job", "step", 1, default);
        client.Received(1).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineRunJob)), Arg.Any<EnqueuedState>());
        (await runtime.GetExecutionAsync(run.Id))!.Steps[0].Status.Should().Be(StepExecutionStatus.Succeeded);
    }

    [TestMethod]
    public async Task Missing_run_compatibility_entry_point_does_not_schedule()
    {
        using var provider = Services();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PipelineRunJob>().ExecuteAsync(Guid.NewGuid(), true, default);
        client.ReceivedCalls().Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task Waiting_run_schedules_due_time_but_recovery_does_not_duplicate_wakeup(bool recovery)
    {
        var clock = new TestClock();
        using var gate = new PipelineOperationGate();
        var memory = new InMemoryPipelineStore(clock, gate);
        var run = Samples.Run() with { Status = PipelineStatus.Running };
        await memory.CreateAsync(run, Samples.Request());
        var snapshot = (await memory.LoadAsync(run.Id, default))!;
        var due = clock.Now.AddMinutes(1);
        await memory.TrySaveAsync(snapshot with
        {
            Jobs = [snapshot.Jobs[0] with { Status = JobExecutionStatus.Running }],
            Steps = [snapshot.Steps[0] with { Status = StepExecutionStatus.Waiting, NextAttemptAt = due, PollDeadline = due.AddMinutes(1) }]
        }, 0, "waiting", null, default);
        var definitions = Substitute.For<IPipelineDefinitionRegistry>();
        definitions.Restore(Arg.Any<PipelineExecutionSpecification>()).Returns(Samples.Plan());
        var coordinator = new PipelineExecutionCoordinator(memory, memory, definitions, Substitute.For<IStepArgumentBinder>(), Substitute.For<IStepInvoker>(), clock, gate);
        await new PipelineRunJob(coordinator, memory, client, clock).ExecuteAsync(run.Id, run.Title, recovery, default);
        if (recovery) client.ReceivedCalls().Should().BeEmpty();
        else client.Received(1).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineRunJob)), Arg.Is<ScheduledState>(s => s.EnqueueAt == due.UtcDateTime));
    }

    [TestMethod]
    public async Task Retry_dispatches_reopened_run_and_ignores_ineligible_run()
    {
        using var provider = Services();
        var runtime = provider.GetRequiredService<IPipelineRuntime>();
        var run = await runtime.EnqueueAsync(Samples.Request());
        client.ClearReceivedCalls();
        (await runtime.RetryAsync(run.Id)).Should().BeFalse();
        client.ReceivedCalls().Should().BeEmpty();
        var store = provider.GetRequiredService<IPipelineStore>();
        await store.TryUpdateAsync(run with { Revision = 1, Status = PipelineStatus.Failed }, 0);
        (await runtime.RetryAsync(run.Id)).Should().BeTrue();
        client.Received(1).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineRunJob) && (Guid)j.Args[0] == run.Id), Arg.Any<EnqueuedState>());
    }
    [TestMethod]
    [DataRow(PipelineStatus.Completed)] [DataRow(PipelineStatus.Failed)] [DataRow(PipelineStatus.Cancelled)]
    public async Task Terminal_run_jobs_do_not_dispatch_more_work(PipelineStatus status)
    {
        using var provider = Services();
        var runtime = provider.GetRequiredService<IPipelineRuntime>();
        var run = await runtime.EnqueueAsync(Samples.Request());
        await provider.GetRequiredService<IPipelineStore>().TryUpdateAsync(run with { Status = status, Revision = 1 }, 0);
        client.ClearReceivedCalls();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PipelineRunJob>().ExecuteAsync(run.Id, run.Title, false, default);
        await scope.ServiceProvider.GetRequiredService<PipelineStepJob>().ExecuteAsync(run.Id, run.Title, "job", "step", 1, default);
        client.ReceivedCalls().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Retry_remains_accepted_when_dispatch_fails()
    {
        using var provider = Services();
        var runtime = provider.GetRequiredService<IPipelineRuntime>();
        var run = await runtime.EnqueueAsync(Samples.Request());
        await provider.GetRequiredService<IPipelineStore>().TryUpdateAsync(run with { Status = PipelineStatus.Failed, Revision = 1 }, 0);
        client.Create(Arg.Any<Job>(), Arg.Any<IState>()).Returns(_ => throw new InvalidOperationException("offline"));
        (await runtime.RetryAsync(run.Id)).Should().BeTrue();
        (await runtime.GetRunAsync(run.Id))!.Status.Should().Be(PipelineStatus.Running);
    }

    [TestMethod]
    public async Task Compatibility_signatures_coordinate_existing_run()
    {
        using var provider = Services();
        var run = await provider.GetRequiredService<IPipelineRuntime>().EnqueueAsync(Samples.Request());
        client.ClearReceivedCalls();
        using var scope = provider.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PipelineRunJob>();
        await job.ExecuteAsync(run.Id, false, default);
        await job.ExecuteAsync(run.Id, run.Title, "ignored", "ignored", false, default);
        client.Received(2).Create(Arg.Is<Job>(j => j.Type == typeof(PipelineStepJob)), Arg.Any<EnqueuedState>());
    }
    private ServiceProvider Services()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddPipeline(options => options.UseHangfire());
        services.AddSingleton(client);
        var registration = Substitute.For<IPipelineDefinitionRegistration>();
        registration.DefinitionId.Returns("test"); registration.DefinitionVersion.Returns(2);
        registration.Restore(Arg.Any<PipelineExecutionSpecification>()).Returns(Samples.Plan());
        services.AddSingleton(registration);
        var step = Substitute.For<ITestStep>();
        step.Produce(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("output");
        services.AddSingleton(step);
        return services.BuildServiceProvider();
    }
}



