using Microsoft.Extensions.DependencyInjection;
using Pipeline.Core;
using Pipeline.Tests.Shared;

namespace Pipeline.Runtime.Tests;

[TestClass]
public class RuntimeServicesTests
{
    [TestMethod]
    public void Registration_shares_memory_store_and_preserves_custom_clock()
    {
        var clock = new TestClock();
        var services = new ServiceCollection().AddLogging().AddSingleton<TimeProvider>(clock).AddPipeline();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPipelineStore>().Should().BeSameAs(provider.GetRequiredService<IPipelineExecutionStore>())
            .And.BeSameAs(provider.GetRequiredService<IStepOutputReader>()).And.BeSameAs(provider.GetRequiredService<IPipelineResetService>());
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(clock);
        services.Invoking(x => x.AddPipeline()).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Configuration_freezes_after_callback_and_processor_sees_selected_persistence()
    {
        PipelineRegistrationOptions? saved = null;
        PipelinePersistenceConfiguration? seen = null;
        new ServiceCollection().AddPipeline(options =>
        {
            saved = options;
            options.SelectProcessor((_, persistence) => seen = persistence);
        });
        seen.Should().Be(PipelinePersistenceConfiguration.InMemory);
        saved.Invoking(x => x!.SelectProcessor((_, _) => { })).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Registry_selects_exact_version_and_rejects_missing_or_duplicate_registration()
    {
        var first = Substitute.For<IPipelineDefinitionRegistration>();
        first.DefinitionId.Returns("test"); first.DefinitionVersion.Returns(1);
        var second = Substitute.For<IPipelineDefinitionRegistration>();
        second.DefinitionId.Returns("test"); second.DefinitionVersion.Returns(2);
        var plan = Samples.Plan();
        var specification = new PipelineExecutionSpecification("test", 2, "{}", "{}");
        second.Restore(specification).Returns(plan);
        var registry = new PipelineDefinitionRegistry([first, second]);
        registry.Restore(specification).Should().BeSameAs(plan);
        first.DidNotReceive().Restore(Arg.Any<PipelineExecutionSpecification>());
        registry.Invoking(x => x.Restore(specification with { DefinitionVersion = 3 })).Should().Throw<InvalidOperationException>();
        Action duplicate = () => new PipelineDefinitionRegistry([first, first]);
        duplicate.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public async Task Binder_serializes_captured_values_and_rejects_unavailable_output()
    {
        var reader = Substitute.For<IStepOutputReader>();
        var binder = new StepArgumentBinder(reader);
        var plan = Samples.Plan();
        (await binder.BindAsync(Guid.NewGuid(), plan.Jobs[0].Steps[0], default)).ArgumentsJson.Should().Be("[\"hello\"]");
        var builder = new PipelineBuilderFactory().Create(plan.Definition);
        var job = builder.AddJob("job");
        var output = job.Step<ITestStep>("p").Produces((s, ct) => s.Produce("hello", ct));
        job.Step<ITestStep>("c").Using(output).Execute((s, x, ct) => s.Execute(x, ct));
        var definition = builder.Build().Jobs[0].Steps[1];
        foreach (var stored in new StoredStepOutput?[] { null, new(false, true, "\"x\""), new(true, false, "\"x\""), new(true, true, null) })
        {
            reader.ReadAsync(Arg.Any<Guid>(), "job", "p", Arg.Any<CancellationToken>()).Returns(stored);
            Func<Task> bind = () => binder.BindAsync(Guid.NewGuid(), definition, default);
            await bind.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not available*");
        }
    }

    [TestMethod]
    public async Task Gate_serializes_operations_and_releases_after_exception()
    {
        using var gate = new PipelineOperationGate();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = gate.ExecuteAsync(async () => { entered.SetResult(); await release.Task; return 1; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondEntered = false;
        var second = gate.ExecuteAsync(() => { secondEntered = true; return Task.FromResult(2); });
        secondEntered.Should().BeFalse();
        release.SetResult();
        (await first).Should().Be(1); (await second).Should().Be(2);
        Func<Task> failed = () => gate.ExecuteAsync<int>(() => throw new InvalidOperationException("failure"));
        await failed.Should().ThrowAsync<InvalidOperationException>();
        (await gate.ExecuteAsync(() => Task.FromResult(3))).Should().Be(3);
    }

    [TestMethod]
    public async Task Logger_rejects_use_outside_invocation()
    {
        var logger = new PipelineStepLogger(Substitute.For<IPipelineExecutionStore>(), new TestClock());
        Func<Task> log = () => logger.InformationAsync("hello");
        await log.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active step invocation*");
    }
}
