using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Pipeline.Blazor.Components;
using Pipeline.Blazor.Pages;
using Pipeline.Contracts;

namespace Pipeline.Blazor.Tests;

[TestClass]
public class LiveComponentTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(StepExecutionStatus.Waiting, 1, 10, "Recheck in 1 second.")]
    [DataRow(StepExecutionStatus.Waiting, 5, 10, "Recheck in 5 seconds.")]
    [DataRow(StepExecutionStatus.Waiting, 0, 10, "Recheck due; awaiting status update.")]
    [DataRow(StepExecutionStatus.Waiting, 10, 10, "Polling timeout in 10 seconds.")]
    [DataRow(StepExecutionStatus.Waiting, 1, 1, "Polling timeout in 1 second.")]
    [DataRow(StepExecutionStatus.Waiting, 5, 0, "Polling deadline reached; awaiting status update.")]
    [DataRow(StepExecutionStatus.Running, 5, 10, "Verification in progress…")]
    public async Task Job_cards_show_poll_countdown(
        StepExecutionStatus status,
        int dueSeconds,
        int deadlineSeconds,
        string expected)
    {
        //Arrange
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);

        await using var context = new BunitContext();
        context.Services.AddSingleton(clock);

        var id = Guid.NewGuid();
        var job = new PipelineJobView
        {
            RunId = id,
            JobId = "job",
            Status = JobExecutionStatus.Running,
            StartedAt = now
        };
        var step = new PipelineStepView
        {
            RunId = id,
            JobId = "job",
            StepId = "poll",
            Status = status,
            NextAttemptAt = now.AddSeconds(dueSeconds),
            PollDeadline = now.AddSeconds(deadlineSeconds)
        };

        // Act
        var cards = context.Render<PipelineJobCards>(parameters => parameters
            .Add(x => x.Jobs, [job])
            .Add(x => x.Steps, [step]));

        // Assert
        cards.Find(".job-card").TextContent.Should().Contain(expected);

        cards.Render(parameters => parameters
            .Add(x => x.Jobs, [job with { Status = JobExecutionStatus.Failed }]));

        cards.Find(".job-card").TextContent.Should().NotContain(expected);
    }

    public sealed class TestPage : LivePipelinePage
    {
        public Func<CancellationToken, Task> Load { get; set; } = _ => Task.CompletedTask;

        public Task Reload(CancellationToken token = default) => ReloadAsync(token);

        /// <summary>
        /// Gets whether snapshot loading completed successfully.
        /// </summary>
        public bool Loaded => HasLoaded;

        /// <summary>
        /// Gets the current user-facing loading error.
        /// </summary>
        public string? LoadingError => RefreshError;

        protected override Task LoadSnapshotAsync(CancellationToken cancellationToken) => Load(cancellationToken);
    }

    [TestMethod]
    public async Task Page_serializes_reload_and_disposal_waits_for_inflight_load()
    {
        // Arrange
        var token = TestContext.CancellationToken;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        var page = new TestPage
        {
            Load = async cancellationToken =>
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            }
        };

        Task? disposal = null;

        try
        {
            // Act
            var first = page.Reload(token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

            var second = page.Reload(token);

            // Assert
            calls.Should().Be(1);

            disposal = page.DisposeAsync().AsTask();
            disposal.IsCompleted.Should().BeFalse();

            release.TrySetResult();

            await Task.WhenAll(first, second, disposal)
                .WaitAsync(TimeSpan.FromSeconds(5), token);

            calls.Should().Be(2);
        }
        finally
        {
            // Unblock any load before waiting for cleanup, even if an assertion fails.
            release.TrySetResult();

            if (disposal is not null)
            {
                await disposal;
            }
            else
            {
                await page.DisposeAsync();
            }
        }
    }

    [TestMethod]
    public async Task Cancelled_reload_does_not_load_or_throw()
    {
        await using var page = new TestPage
        {
            Load = _ => throw new AssertFailedException("Unexpected load")
        };

        await page.Reload(new CancellationToken(true));
    }

    [TestMethod]
    public async Task Failed_reload_preserves_data_and_later_reload_recovers()
    {
        // Arrange
        var attempts = 0;
        var publishedValue = 0;

        await using var page = new TestPage
        {
            Load = _ =>
            {
                attempts++;

                if (attempts == 2)
                {
                    throw new HttpRequestException(
                        "private connection detail");
                }

                publishedValue = attempts;
                return Task.CompletedTask;
            }
        };

        // Act
        await page.Reload(TestContext.CancellationToken);
        await page.Reload(TestContext.CancellationToken);

        // Assert
        page.Loaded.Should().BeTrue();
        page.LoadingError.Should().NotBeNull();
        publishedValue.Should().Be(1);

        // Act
        await page.Reload(TestContext.CancellationToken);

        // Assert
        page.Loaded.Should().BeTrue();
        page.LoadingError.Should().BeNull();
        publishedValue.Should().Be(3);
    }
}