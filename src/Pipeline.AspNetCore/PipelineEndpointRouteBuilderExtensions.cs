using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pipeline.Contracts;

namespace Pipeline.AspNetCore;

/// <summary>
/// Maps pipeline monitoring endpoints into an ASP.NET Core application.
/// </summary>
public static class PipelineEndpointRouteBuilderExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps run summaries, execution queries, and retry requests.
    /// </summary>
    /// <param name="endpoints">
    /// The application or route group receiving the endpoints.
    /// </param>
    /// <returns>
    /// The pipeline route group for further endpoint configuration.
    /// </returns>
    /// <remarks>
    /// Requires an IPipelineMonitor registration, normally supplied by
    /// AddPipeline in the backend host.
    /// Routes use the /api/pipeline prefix relative to the supplied builder.
    /// Apply host authorization policies to the returned group.
    /// Fixed JSON options keep this protocol independent of unrelated
    /// application JSON configuration.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// The endpoint builder is null.
    /// </exception>
    public static RouteGroupBuilder MapPipelineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/pipeline");

        group.AddEndpointFilter(HandleRequestAsync);

        group.MapGet("/runs", ListAsync);
        group.MapGet("/runs/{runId:guid}/execution", GetExecutionAsync);
        group.MapPost("/runs/{runId:guid}/retry", RetryAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        IPipelineMonitor monitor,
        CancellationToken cancellationToken,
        int skip = 0,
        int take = 50)
    {
        if (skip < 0 || take is < 1 or > 100)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid paging parameters.",
                detail: "skip must be nonnegative and take must be 1–100.");
        }

        var runs = await monitor.GetRunsAsync(
            skip,
            take,
            cancellationToken);

        return Results.Json(runs, JsonOptions);
    }

    private static async Task<IResult> GetExecutionAsync(
        Guid runId,
        IPipelineMonitor monitor,
        CancellationToken cancellationToken)
    {
        var execution = await monitor.GetExecutionAsync(runId, cancellationToken);

        if (execution is null)
        {
            return Results.NotFound();
        }

        return Results.Json(execution, JsonOptions);
    }

    private static async Task<IResult> RetryAsync(
        Guid runId,
        IPipelineMonitor monitor,
        CancellationToken cancellationToken)
    {
        var accepted = await monitor.RetryAsync(runId, cancellationToken);

        if (!accepted)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Retry was not accepted.",
                detail: "The run is missing, ineligible, or changed concurrently.");
        }

        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async ValueTask<object?> HandleRequestAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Monitoring responses must not be reused as cached live state.
        httpContext.Response.Headers.CacheControl = "no-store";

        try
        {
            return await next(context);
        }
        catch (OperationCanceledException)
            when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Pipeline.AspNetCore");

            logger.LogError(
                exception,
                "Pipeline request failed: {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);

            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The pipeline request could not be completed.");
        }
    }
}