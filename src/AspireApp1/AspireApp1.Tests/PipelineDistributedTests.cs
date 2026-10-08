using System.Net.Http.Json;

using Pipeline.Contracts;

namespace AspireApp1.Tests;

/// <summary>
/// Verifies pipeline monitoring across the Aspire application boundary.
/// </summary>
/// <remarks>
/// The API service executes the demonstration pipeline.
/// The web frontend renders its state through the HTTP monitoring client.
/// This test covers initial server rendering; browser interaction and
/// subsequent interactive refreshes require browser verification.
/// </remarks>
[TestClass]
public sealed class PipelineDistributedTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets or sets the context supplied by MSTest.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that the frontend displays a run executed by the API service.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [TestMethod]
    public async Task Frontend_displays_backend_pipeline_execution()
    {
        // Arrange
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        timeoutCancellation.CancelAfter(DefaultTimeout);

        var cancellationToken = timeoutCancellation.Token;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AspireApp1_AppHost>(cancellationToken);

        await using var app = await appHost.BuildAsync(cancellationToken);

        await app.StartAsync(cancellationToken);

        await app
            .ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cancellationToken);

        await app
            .ResourceNotifications
            .WaitForResourceHealthyAsync("webfrontend", cancellationToken);

        using var backend = app.CreateHttpClient("apiservice");
        using var frontend = app.CreateHttpClient("webfrontend");

        // Act
        var execution = await WaitForCompletedDemoAsync(backend, cancellationToken);

        using var overviewResponse = await frontend.GetAsync("/pipeline", cancellationToken);

        var overviewHtml = await overviewResponse
            .Content
            .ReadAsStringAsync(cancellationToken);

        using var detailResponse = await frontend.GetAsync($"/pipeline/run/{execution.Run.Id:D}", cancellationToken);

        var detailHtml = await detailResponse
            .Content
            .ReadAsStringAsync(cancellationToken);

        // Assert
        Assert.AreEqual(PipelineStatus.Completed, execution.Run.Status);

        Assert.IsTrue(
            execution.Run.Logs.Any(entry => entry.Message == "=== Formatting samples complete ==="));

        Assert.AreEqual(HttpStatusCode.OK, overviewResponse.StatusCode);

        StringAssert.Contains(overviewHtml, execution.Run.Id.ToString("D"));

        Assert.AreEqual(HttpStatusCode.OK, detailResponse.StatusCode);

        StringAssert.Contains(detailHtml, "Formatting samples complete");
    }

    private static async Task<PipelineExecutionView> WaitForCompletedDemoAsync(HttpClient backend, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var runs = await backend
                .GetFromJsonAsync<PipelineRunSummaryView[]>(
                    "/api/pipeline/runs",
                    cancellationToken)
                ?? throw new InvalidDataException(
                    "The backend returned no run collection.");

            var demo = runs.FirstOrDefault(run => run.Definition.Id == "LogFormatting");

            if (demo is not null)
            {
                var execution = await backend
                    .GetFromJsonAsync<PipelineExecutionView>(
                        $"/api/pipeline/runs/{demo.Id:D}/execution",
                        cancellationToken)
                    ?? throw new InvalidDataException(
                        "The backend returned no execution snapshot.");

                if (execution.Run.Status == PipelineStatus.Completed)
                {
                    return execution;
                }

                if (execution.Run.Status is PipelineStatus.Failed or PipelineStatus.Cancelled)
                {
                    throw new InvalidOperationException(
                        $"The demonstration ended with status " +
                        $"{execution.Run.Status}: " +
                        execution.Run.StatusText);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }
}