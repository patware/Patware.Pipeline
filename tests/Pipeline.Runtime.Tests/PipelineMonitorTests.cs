using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

namespace Pipeline.Runtime.Tests;

/// <summary>
/// Verifies the monitoring boundary exposed by runtime registration.
/// </summary>
[TestClass]
public sealed class PipelineMonitorTests
{
    /// <summary>
    /// Verifies that monitoring responses exclude private execution fields.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [TestMethod]
    public async Task Execution_view_excludes_private_execution_fields()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var run = new PipelineRun
        {
            Id = runId,
            Definition = new("test", "Test"),
            Title = "A run",
            CreatedBy = "tester",
            CreatedAt = now,
            QueuedAt = now,
            Status = PipelineStatus.Running
        };

        var snapshot = new PipelineExecutionSnapshot(
            run,
            [
                new JobExecutionState
                {
                    RunId = runId,
                    JobId = "job",
                    Status = JobExecutionStatus.Running
                }
            ],
            [
                new StepExecutionState
                {
                    RunId = runId,
                    JobId = "job",
                    StepId = "step",
                    Status = StepExecutionStatus.Waiting,
                    NextAttemptAt = now.AddSeconds(5),
                    PollDeadline = now.AddMinutes(1),
                    ArgumentsJson = "[\"private argument\"]",
                    HasOutput = true,
                    OutputJson = "\"private output\"",
                    LeaseToken = Guid.NewGuid(),
                    LeaseExpiresAt = now.AddMinutes(2),
                    Error = "private exception detail"
                }
            ]);

        var runtime = Substitute.For<IPipelineRuntime>();
        runtime.GetExecutionAsync(
                runId,
                Arg.Any<CancellationToken>())
            .Returns(snapshot);

        var services = new ServiceCollection();
        services.AddPipeline();

        // Resolve the monitor through its normal registration,
        // with the selected runtime replaced for this test.
        services.AddSingleton<IPipelineRuntime>(runtime);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var monitor = scope.ServiceProvider
            .GetRequiredService<Contracts.IPipelineMonitor>();

        // Act
        var view = await monitor.GetExecutionAsync(runId);

        var json = JsonSerializer.Serialize(
            view,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var step = document.RootElement.GetProperty("steps")[0];

        // Assert
        view.Should().NotBeNull();
        view!.Steps[0].Status.Should()
            .Be(Contracts.StepExecutionStatus.Waiting);
        view.Steps[0].NextAttemptAt.Should()
            .Be(now.AddSeconds(5));

        foreach (var name in new[]
        {
            "argumentsJson",
            "outputJson",
            "hasOutput",
            "leaseToken",
            "leaseExpiresAt",
            "revision",
            "error"
        })
        {
            step.TryGetProperty(name, out _).Should().BeFalse();
        }

        json.Should().NotContain("private argument")
            .And.NotContain("private output")
            .And.NotContain("private exception detail");
    }

    /// <summary>
    /// Verifies that retry uses the selected runtime and caller token.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [TestMethod]
    public async Task Retry_delegates_to_the_selected_runtime()
    {
        // Arrange
        var runId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        var runtime = Substitute.For<IPipelineRuntime>();
        runtime.RetryAsync(runId, cancellation.Token).Returns(true);

        var services = new ServiceCollection();
        services.AddPipeline();
        services.AddSingleton<IPipelineRuntime>(runtime);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var monitor = scope.ServiceProvider
            .GetRequiredService<Contracts.IPipelineMonitor>();

        // Act
        var accepted = await monitor.RetryAsync(
            runId,
            cancellation.Token);

        // Assert
        accepted.Should().BeTrue();

        await runtime.Received(1).RetryAsync(
            runId,
            cancellation.Token);
    }
}