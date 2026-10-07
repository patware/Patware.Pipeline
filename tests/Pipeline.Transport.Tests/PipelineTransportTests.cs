using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Pipeline.AspNetCore;
using Pipeline.Contracts;
using Pipeline.HttpClient;

namespace Pipeline.Transport.Tests;

/// <summary>
/// Verifies the pipeline endpoints and HTTP client together.
/// </summary>
/// <remarks>
/// Uses an in-process HTTP server to exercise routing, serialization,
/// registration, and status handling without Aspire infrastructure.
/// </remarks>
[TestClass]
public sealed class PipelineTransportTests
{
    /// <summary>
    /// Gets or sets the context supplied by MSTest.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that summary responses round-trip through the HTTP client.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [TestMethod]
    public async Task Summaries_round_trip_through_http()
    {
        // Arrange
        var token = TestContext.CancellationToken;
        var runId = Guid.NewGuid();

        var summary = new PipelineRunSummaryView
        {
            Id = runId,
            Definition = new("test", "Test"),
            Title = "A run",
            CreatedBy = "tester",
            CreatedAt = DateTimeOffset.UtcNow,
            Status = PipelineStatus.Running,
            Jobs =
            [
                new PipelineJobView
                {
                    RunId = runId,
                    JobId = "job",
                    Status = JobExecutionStatus.Running
                }
            ]
        };

        var backend = Substitute.For<IPipelineMonitor>();

        backend.GetRunsAsync(0, 50, Arg.Any<CancellationToken>())
            .Returns([summary]);

        await using var app = CreateServer(backend);
        await app.StartAsync(token);

        await using var provider = CreateClientProvider(app);
        var client = provider.GetRequiredService<IPipelineMonitor>();

        // Act
        var runs = await client.GetRunsAsync(
            cancellationToken: token);

        // Assert
        runs.Should().ContainSingle();
        runs[0].Id.Should().Be(runId);
        runs[0].Status.Should().Be(PipelineStatus.Running);
        runs[0].Jobs.Should().ContainSingle();
        runs[0].Jobs[0].JobId.Should().Be("job");

        await backend.Received(1).GetRunsAsync(0, 50, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that a missing execution becomes a null monitor result.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [TestMethod]
    public async Task Missing_execution_returns_null()
    {
        // Arrange
        var token = TestContext.CancellationToken;
        var backend = Substitute.For<IPipelineMonitor>();

        await using var app = CreateServer(backend);
        await app.StartAsync(token);

        await using var provider = CreateClientProvider(app);
        var client = provider.GetRequiredService<IPipelineMonitor>();

        // Act
        var execution = await client.GetExecutionAsync(Guid.NewGuid(), token);

        // Assert
        execution.Should().BeNull();
    }

    /// <summary>
    /// Verifies that a failed retry request is not automatically replayed.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [TestMethod]
    public async Task Failed_retry_is_not_replayed()
    {
        // Arrange
        var token = TestContext.CancellationToken;
        var runId = Guid.NewGuid();
        var backend = Substitute.For<IPipelineMonitor>();

        backend.RetryAsync(runId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(
                new InvalidOperationException("private server detail")));

        await using var app = CreateServer(backend);
        await app.StartAsync(token);

        await using var provider = CreateClientProvider(app);
        var client = provider.GetRequiredService<IPipelineMonitor>();

        Func<Task> retry = async () =>
        {
            await client.RetryAsync(runId, token);
        };

        // Act
        var failure = await retry.Should()
            .ThrowAsync<HttpRequestException>();

        // Assert
        failure.Which.StatusCode.Should()
            .Be(HttpStatusCode.InternalServerError);

        backend.ReceivedCalls()
            .Count(call =>
                call.GetMethodInfo().Name ==
                nameof(IPipelineMonitor.RetryAsync))
            .Should().Be(1);
    }

    private static WebApplication CreateServer(IPipelineMonitor backend)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPipelineMonitor>(backend);

        var app = builder.Build();
        app.MapPipelineEndpoints();

        return app;
    }

    private static ServiceProvider CreateClientProvider(WebApplication app)
    {
        var services = new ServiceCollection();

        services.AddPipelineClient(new Uri("http://localhost/"))
            .ConfigurePrimaryHttpMessageHandler(
                () => app.GetTestServer().CreateHandler());

        return services.BuildServiceProvider();
    }
}