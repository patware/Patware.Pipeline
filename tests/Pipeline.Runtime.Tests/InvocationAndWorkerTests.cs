using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Pipeline.Core;
using Pipeline.Core.Pipelines;
using Pipeline.Runtime.Pipelines;
using Pipeline.Tests.Shared;

namespace Pipeline.Runtime.Tests;

[TestClass]
public class InvocationAndWorkerTests
{
    public sealed class LoggingStep(IPipelineStepLogger logger)
    {
        public async Task Execute(CancellationToken token)
        {
            await logger.InformationAsync("info", token);
            await logger.WarningAsync("warning", token);
            await logger.ErrorAsync("error", token);
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "Intentionally models an instance pipeline step to test synchronous exception propagation.")]
        public Task Throw(CancellationToken token) => throw new ApplicationException("original exception");
    }

    [TestMethod]
    public async Task Invocation_scopes_logger_to_claim_and_persists_severities()
    {
        var clock = new TestClock();
        using var provider = new ServiceCollection().AddLogging().AddSingleton<TimeProvider>(clock)
            .AddPipeline().AddScoped<LoggingStep>().BuildServiceProvider();
        var builder = new PipelineBuilderFactory().Create(new("logging", "Logging"));
        builder.AddJob("job").Step<LoggingStep>("step").Execute((s, ct) => s.Execute(ct));
        var plan = builder.Build();
        var runtime = provider.GetRequiredService<IPipelineRuntime>();
        var run = await runtime.EnqueueAsync(Samples.Request(plan));
        var store = provider.GetRequiredService<IPipelineExecutionStore>();
        var snapshot = (await store.LoadAsync(run.Id, default))!;
        var claim = new StepClaim(run.Id, "job", "step", Guid.NewGuid(), clock.Now.AddMinutes(1));
        await store.TrySaveAsync(snapshot with { Steps = [snapshot.Steps[0] with { Status = StepExecutionStatus.Running, LeaseToken = claim.LeaseToken, LeaseExpiresAt = claim.LeaseExpiresAt }] }, 0, "claimed", null, default);
        using var scope = provider.CreateScope();
        var invoker = scope.ServiceProvider.GetRequiredService<IStepInvoker>();
        var result = await invoker.InvokeAsync(plan.Jobs[0].Steps[0], new("[]"), claim, default);
        result.Should().Be(new StepInvocationResult(false, null, null));
        var logs = (await store.LoadAsync(run.Id, default))!.Run.Logs.TakeLast(3).ToArray();
        logs.Select(x => x.Level).Should().Equal(PipelineLogLevel.Information, PipelineLogLevel.Warning, PipelineLogLevel.Error);
        logs.Should().OnlyContain(x => x.JobId == "job" && x.StepId == "step");
        clock.Now = claim.LeaseExpiresAt;
        await FluentActions.Awaiting(() => invoker.InvokeAsync(plan.Jobs[0].Steps[0], new("[]"), claim, default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*losing step ownership*");
    }

    [TestMethod]
    public async Task Invocation_unwraps_synchronous_service_exception_and_rejects_invalid_arguments()
    {
        using var provider = new ServiceCollection().AddLogging().AddPipeline().AddScoped<LoggingStep>().BuildServiceProvider();
        var builder = new PipelineBuilderFactory().Create(new("throw", "Throw"));
        builder.AddJob("job").Step<LoggingStep>("step").Execute((s, ct) => s.Throw(ct));
        var definition = builder.Build().Jobs[0].Steps[0];
        var claim = new StepClaim(Guid.NewGuid(), "job", "step", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1));
        var invoker = new StepInvoker(provider);
        await FluentActions.Awaiting(() => invoker.InvokeAsync(definition, new("[]"), claim, default)).Should().ThrowAsync<ApplicationException>().WithMessage("original exception");
        foreach (var json in new[] { "{}", "[1]" })
            await FluentActions.Awaiting(() => invoker.InvokeAsync(definition, new(json), claim, default)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*arguments*");
    }

    [TestMethod]
    public void Formatting_registration_restores_and_validates_specification()
    {
        var registration = new LogFormattingRegistration(new LogFormattingPipeline(new PipelineBuilderFactory()));
        var spec = new PipelineExecutionSpecification(registration.DefinitionId, registration.DefinitionVersion, "{}", "{}");
        registration.Restore(spec).Definition.Should().Be(LogFormattingPipeline.Definition);
        foreach (var invalid in new[] { spec with { DefinitionId = "other" }, spec with { DefinitionVersion = 99 }, spec with { InputJson = "null" }, spec with { SettingsJson = "null" } })
            registration.Invoking(x => x.Restore(invalid)).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public async Task Background_worker_forwards_shutdown_cancellation()
    {
        var runtime = Substitute.For<IPipelineRuntime>();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.RunAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var token = call.Arg<CancellationToken>();
            started.SetResult(token);
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
        });
        using var worker = new PipelineWorker(runtime);
        await worker.StartAsync(default);
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(default);
        token.IsCancellationRequested.Should().BeTrue();
        await runtime.Received(1).RunAsync(token);
    }

    [TestMethod]
    public async Task Runner_handles_discovery_failure_and_shutdown_and_cannot_start_twice()
    {
        using var gate = new PipelineOperationGate();
        using var cancel = new CancellationTokenSource();
        var execution = Substitute.For<IPipelineExecutionStore>();

        execution.ListActiveRunIdsAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancel.Cancel();
            return Task.FromException<IReadOnlyList<Guid>>(new InvalidOperationException("offline"));
        });

        var runtime = new PipelineRuntime(
            Substitute.For<IPipelineStore>(),
            execution,
            Substitute.For<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<PipelineRuntime>.Instance,
            gate,
            new PipelineRunEventQueue());

        await runtime.RunAsync(cancel.Token);

        await execution.Received(1).ListActiveRunIdsAsync(cancel.Token);

        await FluentActions
            .Awaiting(() => runtime.RunAsync(default))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been started*");
    }
}

