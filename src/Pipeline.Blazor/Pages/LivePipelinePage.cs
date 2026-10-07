using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

using Pipeline.Contracts;

namespace Pipeline.Blazor.Pages;

/// <summary>
/// Serializes snapshot loading and refreshes pipeline pages periodically.
/// </summary>
/// <remarks>Snapshot failures are handled at the page boundary and logged. The timer remains active. Exceptions are not displayed verbatim.
/// </remarks>
public abstract class LivePipelinePage : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// Gets the service used to query runs and request retries.
    /// </summary>
    [Inject]
    protected IPipelineMonitor Monitor { get; set; } = default!;

    /// <summary>
    /// Gets the logger used to record snapshot-loading failures.
    /// </summary>
    /// <remarks>
    /// The null-conditional logger call also accommodates your existing tests that construct TestPage directly without component injection.
    /// </remarks>
    [Inject]
    protected ILogger<LivePipelinePage> Logger { get; set; } = default!;

    private readonly CancellationTokenSource _refreshCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private Task? _refreshTask;

    /// <summary>
    /// The parameter version prevents a slow operation for an earlier route from changing the loading/error state of the new route. Derived pages must still discard their stale data, as your detail page already does.
    /// </summary>
    private long _parameterVersion;

    /// <summary>
    /// Gets whether a snapshot load completed successfully for the
    /// current component parameters.
    /// </summary>
    /// <remarks>
    /// A successful load can report that the requested run does not exist.    
    /// </remarks>
    protected bool HasLoaded { get; private set; }

    /// <summary>
    /// Gets the user-facing message for the latest loading failure,
    /// or null when no loading error is present.
    /// </summary>
    protected string? RefreshError { get; private set; }

    /// <summary>
    /// Gets the interval between refresh attempts.
    /// </summary>
    protected virtual TimeSpan RefreshInterval => TimeSpan.FromSeconds(20);

    /// <summary>
    /// Loads and publishes the page's current snapshot.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token used to cancel loading.
    /// </param>
    /// <returns>A task representing snapshot loading.</returns>
    /// <remarks>
    /// Publish replacement data only after loading succeeds.
    /// Discard results when route parameters changed during loading.
    /// </remarks>
    protected abstract Task LoadSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads a snapshot when component parameters change.
    /// </summary>
    /// <returns>A task representing the serialized load.</returns>
    protected override Task OnParametersSetAsync()
    {
        _parameterVersion++;
        HasLoaded = false;
        RefreshError = null;

        return ReloadAsync(_refreshCancellation.Token);
    }

    /// <summary>
    /// Starts periodic refresh after the first render.
    /// </summary>
    /// <param name="firstRender">
    /// Whether this is the component's first render.
    /// </param>
    /// <returns>A completed lifecycle task.</returns>
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _refreshTask = RefreshAsync(_refreshCancellation.Token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Serializes loading and records failures while preserving
    /// the previously published snapshot.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes after loading, failure handling,
    /// or cancellation.
    /// </returns>
    protected async Task ReloadAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _refreshGate.WaitAsync(cancellationToken);

            try
            {
                var requestedVersion = _parameterVersion;

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await LoadSnapshotAsync(cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (requestedVersion == _parameterVersion)
                    {
                        HasLoaded = true;
                        RefreshError = null;
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    // Cancellation does not represent a loading failure.
                }
                catch (Exception exception)
                {
                    Logger?.LogError(
                        exception,
                        "Could not load the pipeline page snapshot.");

                    if (requestedVersion == _parameterVersion)
                    {
                        RefreshError =
                            "Could not refresh pipeline data. " +
                            "The page will try again automatically.";
                    }
                }
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Loading was cancelled while waiting for another load.
        }
    }

    private async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    await ReloadAsync(cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        timer.Period = RefreshInterval;
                        StateHasChanged();
                    }
                });
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // The component was disposed.
        }
    }

    /// <summary>
    /// Releases page resources and suppresses finalization.
    /// </summary>
    /// <returns>A task representing asynchronous cleanup.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Cancels refresh and waits for outstanding loads before
    /// releasing the cancellation source.
    /// </summary>
    /// <returns>A task representing resource cleanup.</returns>
    /// <remarks>
    /// Overrides must call and await the base implementation.
    /// </remarks>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        _refreshCancellation.Cancel();

        try
        {
            if (_refreshTask is not null)
            {
                await _refreshTask;
            }
        }
        finally
        {
            try
            {
                await _refreshGate.WaitAsync();
                _refreshGate.Release();
            }
            finally
            {
                _refreshCancellation.Dispose();
            }
        }
    }
}