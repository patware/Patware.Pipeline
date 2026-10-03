using Pipeline.Core.Pipelines;
using Pipeline.Core.Steps;

namespace Pipeline.Core.Tests;

[TestClass]
public class ExamplePipelineTests
{
    [TestMethod]
    public async Task Example_step_uppercases_and_noop_preserves_input()
    {
        (await new ExampleStep().ExecuteAsync("Hello", default)).Should().Be("HELLO");
        (await new NoOpStep().ExecuteAsync("Hello", default)).Should().Be("Hello");
        await FluentActions.Awaiting(() => new ExampleStep().ExecuteAsync("x", new CancellationToken(true))).Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => new NoOpStep().ExecuteAsync("x", new CancellationToken(true))).Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public void Formatting_pipeline_builds_restorable_command_request()
    {
        var request = new LogFormattingPipeline(new PipelineBuilderFactory()).Build("tester");
        request.Definition.Should().Be(LogFormattingPipeline.Definition);
        request.CreatedBy.Should().Be("tester");
        request.InputJson.Should().Be("{}");
        request.SettingsJson.Should().Be("{}");
        request.Plan.Jobs.Should().ContainSingle().Which.Steps.Should().ContainSingle().Which
            .ServiceType.Should().Be(typeof(LogFormattingStep));
    }

    [TestMethod]
    public async Task Formatting_step_writes_all_severities_and_finishes_after_examples()
    {
        var logger = Substitute.For<IPipelineStepLogger>();
        using var cancellation = new CancellationTokenSource();
        await new LogFormattingStep(logger).ExecuteAsync(cancellation.Token);
        await logger.Received().InformationAsync(Arg.Is<string>(x => x.Contains("\u001b[")), cancellation.Token);
        await logger.Received().WarningAsync(Arg.Any<string>(), cancellation.Token);
        await logger.Received().ErrorAsync(Arg.Any<string>(), cancellation.Token);
        logger.ReceivedCalls().Last().GetArguments()[0].Should().Be("=== Formatting samples complete ===");
    }
}
