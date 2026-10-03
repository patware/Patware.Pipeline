using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Pipeline.Blazor.Components;
using Pipeline.Blazor.Pages;
using Pipeline.Runtime;

namespace Pipeline.Blazor.Tests;

[TestClass]
public class LiveComponentTests
{
    [TestMethod]
    [DataRow(StepExecutionStatus.Waiting, 1, 10, "Recheck in 1 second.")]
    [DataRow(StepExecutionStatus.Waiting, 5, 10, "Recheck in 5 seconds.")]
    [DataRow(StepExecutionStatus.Waiting, 0, 10, "Recheck due; awaiting status update.")]
    [DataRow(StepExecutionStatus.Waiting, 10, 10, "Polling timeout in 10 seconds.")]
    [DataRow(StepExecutionStatus.Waiting, 1, 1, "Polling timeout in 1 second.")]
    [DataRow(StepExecutionStatus.Waiting, 5, 0, "Polling deadline reached; awaiting status update.")]
    [DataRow(StepExecutionStatus.Running, 5, 10, "Verification in progress…")]
    public void Job_cards_show_poll_countdown(StepExecutionStatus status, int dueSeconds, int deadlineSeconds, string expected)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        using var context = new Bunit.TestContext();
        context.Services.AddSingleton(clock);
        var id = Guid.NewGuid();
        var job = new JobExecutionState { RunId = id, JobId = "job", Status = JobExecutionStatus.Running, StartedAt = now };
        var step = new StepExecutionState { RunId = id, JobId = "job", StepId = "poll", Status = status, NextAttemptAt = now.AddSeconds(dueSeconds), PollDeadline = now.AddSeconds(deadlineSeconds) };
        var cards = context.RenderComponent<PipelineJobCards>(p => p.Add(x => x.Jobs, new[] { job }).Add(x => x.Steps, new[] { step }));
        cards.Find(".job-card").TextContent.Should().Contain(expected);
        cards.SetParametersAndRender(p => p.Add(x => x.Jobs, new[] { job with { Status = JobExecutionStatus.Failed } }));
        cards.Find(".job-card").TextContent.Should().NotContain(expected);
    }

    public sealed class TestPage : LivePipelinePage
    {
        public Func<CancellationToken, Task> Load { get; set; } = _ => Task.CompletedTask;
        public Task Reload(CancellationToken token = default) => ReloadAsync(token);
        protected override Task LoadSnapshotAsync(CancellationToken cancellationToken) => Load(cancellationToken);
    }

    [TestMethod]
    public async Task Page_serializes_reload_and_disposal_waits_for_inflight_load()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var page = new TestPage { Load = async _ => { Interlocked.Increment(ref calls); started.TrySetResult(); await release.Task; } };
        var first = page.Reload();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = page.Reload();
        calls.Should().Be(1);
        var disposal = page.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        release.SetResult();
        await Task.WhenAll(first, second, disposal).WaitAsync(TimeSpan.FromSeconds(5));
        calls.Should().Be(2);
    }

    [TestMethod]
    public async Task Cancelled_reload_does_not_load_or_throw()
    {
        await using var page = new TestPage { Load = _ => throw new AssertFailedException("Unexpected load") };
        await page.Reload(new CancellationToken(true));
    }
}
