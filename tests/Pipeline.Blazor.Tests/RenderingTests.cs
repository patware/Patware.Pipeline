using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Pipeline.Blazor.Components;
using Pipeline.Contracts;

namespace Pipeline.Blazor.Tests;

[TestClass]
public class RenderingTests
{
    private static async Task<string> Render<T>(Dictionary<string, object?>? parameters = null) where T : IComponent
    {
        using var provider = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters ?? []));
            return component.ToHtmlString();
        });
    }

    [TestMethod]
    public async Task Log_text_html_encodes_untrusted_content_and_renders_ansi_styles()
    {
        var html = await Render<AnsiLogText>(new() { [nameof(AnsiLogText.Text)] = "\u001b[31m<script>alert('x')</script>\u001b[0mplain" });
        html.Should().Contain("color:#cd3131;").And.Contain("&lt;script&gt;").And.NotContain("<script>").And.Contain("plain");
    }

    [TestMethod]
    public async Task Empty_jobs_show_helpful_message()
        => (await Render<PipelineJobCards>()).Should().Contain("No job information is available");

    [TestMethod]
    [DataRow(JobExecutionStatus.Pending, "Pending", "job-neutral")]
    [DataRow(JobExecutionStatus.Running, "Running", "job-running")]
    [DataRow(JobExecutionStatus.Succeeded, "Succeeded", "job-succeeded")]
    [DataRow(JobExecutionStatus.Failed, "Failed", "job-failed")]
    [DataRow(JobExecutionStatus.TimedOut, "Timed out", "job-failed")]
    [DataRow(JobExecutionStatus.Blocked, "Blocked", "job-blocked")]
    [DataRow(JobExecutionStatus.ConditionSkipped, "Skipped", "job-neutral")]
    public async Task Job_cards_render_status_and_escape_log_links(JobExecutionStatus status, string label, string css)
    {
        var id = Guid.NewGuid();
        var html = await Render<PipelineJobCards>(new()
        {
            [nameof(PipelineJobCards.Jobs)] = new[] { new PipelineJobView { RunId = id, JobId = "job & one", Status = status } },
            [nameof(PipelineJobCards.Steps)] = new[] { new PipelineStepView { RunId = id, JobId = "job & one", StepId = "step/two", Status = StepExecutionStatus.Pending } }
        });
        html.Should().Contain(label).And.Contain(css).And.Contain("job=job%20%26%20one").And.Contain("step=step%2Ftwo");
    }

    [TestMethod]
    [DataRow(5, "0m 05s")]
    [DataRow(3661, "1h 01m 01s")]
    [DataRow(90061, "1d 01h 01m 01s")]
    [DataRow(-1, "0m 00s")]
    public async Task Completed_job_renders_duration(int seconds, string expected)
    {
        var start = DateTimeOffset.UtcNow;
        var html = await Render<PipelineJobCards>(new()
        {
            [nameof(PipelineJobCards.Jobs)] = new[]
        {
            new PipelineJobView { RunId = Guid.NewGuid(), JobId = "job", Status = JobExecutionStatus.Succeeded, StartedAt = start, FinishedAt = start.AddSeconds(seconds) }
        }
        });
        html.Should().Contain(expected);
    }

    [TestMethod]
    public async Task Js_interop_imports_once_for_multiple_prompts_and_disposes_module()
    {
        var runtime = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        runtime.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]>()).Returns(new ValueTask<IJSObjectReference>(module));
        module.InvokeAsync<string>("showPrompt", Arg.Any<object?[]>()).Returns(new ValueTask<string>("answer"));
        var interop = new ExampleJsInterop(runtime);
        runtime.ReceivedCalls().Should().BeEmpty();
        (await interop.Prompt("question")).Should().Be("answer");
        await interop.Prompt("second");
        await interop.DisposeAsync();
        await runtime.Received(1).InvokeAsync<IJSObjectReference>("import", Arg.Is<object?[]>(args => (string)args[0]! == "./_content/Pipeline.Blazor/exampleJsInterop.js"));
        await module.Received(1).InvokeAsync<string>("showPrompt", Arg.Is<object?[]>(args => (string)args[0]! == "question"));
        await module.Received(1).DisposeAsync();
    }

    [TestMethod]
    public async Task Disposing_unused_interop_does_not_import_module()
    {
        var runtime = Substitute.For<IJSRuntime>();
        await new ExampleJsInterop(runtime).DisposeAsync();
        runtime.ReceivedCalls().Should().BeEmpty();
    }
}
