using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Pipeline.Contracts;

using RunPage = Pipeline.Blazor.Pages.PipelineRun;
using RunsPage = Pipeline.Blazor.Pages.PipelineRuns;

namespace Pipeline.Blazor.Tests;

[TestClass]
public class PageTests
{
    private readonly IPipelineMonitor _monitor = Substitute.For<IPipelineMonitor>();

    private async Task<string> Render<T>(Action<ComponentParameterCollectionBuilder<T>>? parameters = null)
        where T : IComponent
    {
        await using var context = Context();
        return context.Render<T>(parameters).Markup;
    }

    private BunitContext Context()
    {
        var context = new BunitContext();
        context.Services.AddSingleton(_monitor);
        context.Services.AddSingleton(TimeProvider.System);
        return context;
    }


    [TestMethod]
    public async Task Runs_page_renders_empty_state()
    {
        _monitor.GetRunsAsync(0, 50, Arg.Any<CancellationToken>()).Returns([]);
        (await Render<RunsPage>()).Should().Contain("No pipeline runs yet.");
        await _monitor.Received(1).GetRunsAsync(0, 50, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Runs_page_displays_summaries_without_execution_queries()
    {
        // Arrange
        var run = Run(PipelineStatus.Queued);

        var summary = new PipelineRunSummaryView
        {
            Id = run.Id,
            Definition = run.Definition,
            Title = run.Title,
            CreatedBy = run.CreatedBy,
            CreatedAt = run.CreatedAt,
            Status = run.Status,
            StatusText = run.StatusText,
            Jobs =
            [
                new PipelineJobView
            {
                RunId = run.Id,
                JobId = "job",
                Status = JobExecutionStatus.Pending
            }
            ]
        };

        _monitor.GetRunsAsync(
                0,
                50,
                Arg.Any<CancellationToken>())
            .Returns([summary]);

        // Act
        var html = await Render<RunsPage>();

        // Assert
        html.Should().Contain("&lt;Test&gt;")
            .And.Contain("tester")
            .And.Contain($"pipeline/run/{run.Id}")
            .And.Contain("Queued");

        await _monitor.Received(1).GetRunsAsync(
            0,
            50,
            Arg.Any<CancellationToken>());

        await _monitor.DidNotReceive().GetExecutionAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Missing_run_page_displays_not_found()
        => (await Render<RunPage>(
            parameters => parameters.Add(x => x.RunId, Guid.NewGuid())))
            .Should().Contain("Run not found");

    [TestMethod]
    [DataRow(PipelineStatus.Completed, "text-bg-success")]
    [DataRow(PipelineStatus.Failed, "text-bg-danger")]
    [DataRow(PipelineStatus.Running, "text-bg-primary")]
    [DataRow(PipelineStatus.Queued, "text-bg-warning")]
    [DataRow(PipelineStatus.Cancelled, "text-bg-secondary")]
    public async Task Run_page_displays_status_logs_and_retry_only_for_failed_runs(PipelineStatus status, string css)
    {
        // Arrange
        var run = Run(status);
        _monitor.GetExecutionAsync(run.Id, Arg.Any<CancellationToken>()).Returns(new PipelineExecutionView(run, [], []));

        // Act
        var html = await Render<RunPage>(p => p.Add(x => x.RunId, run.Id));

        // Assert
        html.Should().Contain(css).And.Contain("INFO").And.Contain("WARN").And.Contain("ERROR").And.Contain("log-warning").And.Contain("log-error");
        html.Contains("Re-run from failure").Should().Be(status == PipelineStatus.Failed);
    }

    [TestMethod]
    [DataRow("", "run,job,step")]
    [DataRow("?job=job", "job,step")]
    [DataRow("?job=job&step=step", "step")]
    [DataRow("?job=missing", "")]
    [DataRow("?step=step", "")]
    [DataRow("?job=job&step=missing", "")]
    public void Log_query_filters_scope_and_rejects_invalid_selection(string query, string expected)
    {
        // Arrange
        var run = Run(PipelineStatus.Completed) with
        {
            Logs = [
                new(DateTimeOffset.UtcNow, "run"),
                new(DateTimeOffset.UtcNow, "job") { JobId = "job" },
                new(DateTimeOffset.UtcNow, "step") { JobId = "job", StepId = "step" }]
        };

        _monitor
            .GetExecutionAsync(run.Id, Arg.Any<CancellationToken>())
            .Returns(new PipelineExecutionView(run,
                [new() { RunId = run.Id, JobId = "job", Status = JobExecutionStatus.Succeeded }],
                [new() { RunId = run.Id, JobId = "job", StepId = "step", Status = StepExecutionStatus.Succeeded }]));


        using var context = Context();

        context.Services
            .GetRequiredService<NavigationManager>()
            .NavigateTo($"http://localhost/pipeline/run/{run.Id}{query}");

        // Act
        var page = context.Render<RunPage>(p => p.Add(x => x.RunId, run.Id));

        // Assert
        string.Join(",", page.FindAll(".log-message").Select(x => x.TextContent)).Should().Be(expected);

        if (expected.Length == 0) page.Find(".log-empty").TextContent.Should().MatchRegex("(does not exist|Select a job)");
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    public void Retry_button_reports_acceptance_rejection_and_failure(bool accepted, bool throws)
    {
        // Arrange
        var run = Run(PipelineStatus.Failed);

        _monitor
            .GetExecutionAsync(run.Id, Arg.Any<CancellationToken>())
            .Returns(new PipelineExecutionView(run, [], []));

        if (throws)
            _monitor
                .RetryAsync(run.Id, CancellationToken.None)
                .Returns(Task.FromException<bool>(new InvalidOperationException("private server detail")));
        else
            _monitor
                .RetryAsync(run.Id, CancellationToken.None)
                .Returns(accepted);

        using var context = Context();

        // Act
        var page = context.Render<RunPage>(p => p.Add(x => x.RunId, run.Id));

        page.Find("button").Click();

        // Assert
        page.WaitForAssertion(() =>
        {
            page.Find("button").HasAttribute("disabled").Should().BeFalse();

            if (accepted)
                page.FindAll("[role=alert]").Should().BeEmpty();
            else
                page.Find("[role=alert]").TextContent.Should().Contain(throws ? "Could not request a retry" : "no longer eligible");
        });

        _monitor
            .Received(1)
            .RetryAsync(run.Id, CancellationToken.None);

        page.Markup.Should().NotContain("private server detail");
    }
    private static PipelineRunView Run(PipelineStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Definition = new("test", "Test"),
        Title = "<Test>",
        CreatedBy = "tester",
        CreatedAt = DateTimeOffset.UtcNow,
        QueuedAt = DateTimeOffset.UtcNow,
        Status = status,
        Logs = [
            new(DateTimeOffset.UtcNow, "info"),
            new(DateTimeOffset.UtcNow, "warning") { Level = PipelineLogLevel.Warning },
            new(DateTimeOffset.UtcNow, "error") { Level = PipelineLogLevel.Error }]
    };

    [TestMethod]
    public async Task Failed_initial_load_does_not_display_run_not_found()
    {
        // Arrange
        var runId = Guid.NewGuid();

        _monitor
            .GetExecutionAsync(runId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PipelineExecutionView?>(
                new HttpRequestException("private connection detail")));

        // Act
        var html = await Render<RunPage>(parameters => parameters.Add(x => x.RunId, runId));

        // Assert
        html.Should()
            .Contain("Pipeline run unavailable")
            .And.Contain("Could not refresh pipeline data")
            .And.NotContain("Run not found")
            .And.NotContain("private connection detail");
    }
}



