using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Pipeline.Contracts;

namespace Pipeline.HttpClient;

/// <summary>
/// Implements pipeline monitoring through the pipeline HTTP protocol.
/// </summary>
/// <remarks>
/// This implementation is internal so consumers configure it through
/// AddPipelineClient and depend on IPipelineMonitor.
/// </remarks>
internal sealed class HttpPipelineMonitor : IPipelineMonitor
{
    private const string RunsPath = "api/pipeline/runs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly System.Net.Http.HttpClient _httpClient;

    /// <summary>
    /// Initializes the monitor with its configured HTTP client.
    /// </summary>
    /// <param name="httpClient">
    /// The client configured for the pipeline backend.
    /// </param>
    public HttpPipelineMonitor(System.Net.Http.HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PipelineRunSummaryView>> GetRunsAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);

        if (take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        var path = FormattableString.Invariant(
            $"{RunsPath}?skip={skip}&take={take}");

        using var response = await _httpClient.GetAsync(
            path,
            cancellationToken);

        EnsureStatus(response, HttpStatusCode.OK);

        return await ReadRequiredAsync<PipelineRunSummaryView[]>(
            response,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PipelineExecutionView?> GetExecutionAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"{RunsPath}/{runId:D}/execution",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureStatus(response, HttpStatusCode.OK);

        return await ReadRequiredAsync<PipelineExecutionView>(
            response,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RetryAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            $"{RunsPath}/{runId:D}/retry",
            content: null,
            cancellationToken: cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        EnsureStatus(response, HttpStatusCode.Accepted);
        return true;
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        return await response.Content.ReadFromJsonAsync<T>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidDataException(
                "The pipeline service returned a null response.");
    }

    private static void EnsureStatus(
        HttpResponseMessage response,
        HttpStatusCode expected)
    {
        response.EnsureSuccessStatusCode();

        if (response.StatusCode != expected)
        {
            throw new HttpRequestException(
                $"Unexpected pipeline response status: " +
                $"{(int)response.StatusCode}. Expected {(int)expected}.",
                inner: null,
                statusCode: response.StatusCode);
        }
    }
}