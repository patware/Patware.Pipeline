using Microsoft.AspNetCore.Components;

namespace Pipeline.Blazor.Pages;

/// <summary>
/// Provides serialized snapshot loading and periodic refresh for pipeline pages, stopping refresh on disposal.
/// </summary>
public abstract class LivePipelinePage : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// Gets the injected runtime used to query and control pipeline runs.
    /// </summary>
    [Inject]
    protected Pipeline.Runtime.IPipelineRuntime Runtime { get; set; } = default!;

    private readonly CancellationTokenSource _refreshCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private Task? _refreshTask;

    /// <summary>
    /// Gets the periodic refresh interval; the default is twenty seconds.
    /// </summary>
    /// <remarks>Override with a positive interval. The timer starts after the first render and picks up interval changes after each refresh.</remarks>
    protected virtual TimeSpan RefreshInterval => TimeSpan.FromSeconds(20);

    /// <summary>
    /// Loads the page's current data; called by serialized lifecycle and periodic refresh operations.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes after the page snapshot has been updated.</returns>
    /// <remarks>Implementations should observe cancellation and avoid publishing stale data if route parameters changed while loading.</remarks>
    protected abstract Task LoadSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reloads the page snapshot when component parameters change.
    /// </summary>
    /// <returns>A task representing the serialized snapshot load.</returns>
    protected override Task OnParametersSetAsync()
    {
        return ReloadAsync(_refreshCancellation.Token);
    }

    /// <summary>
    /// Starts periodic refresh after the first render.
    /// </summary>
    /// <param name="firstRender">Whether this is the component's first render.</param>
    /// <returns>A completed lifecycle task; the refresh loop runs separately.</returns>
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _refreshTask = RefreshAsync(_refreshCancellation.Token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Serializes snapshot loads and suppresses cancellation requested through the supplied token.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes when loading finishes or the supplied token cancels it.</returns>
    protected async Task ReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _refreshGate.WaitAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LoadSnapshotAsync(cancellationToken);
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // The component was disposed.
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The component was disposed.
        }
    }

    /// <summary>
    /// Cancels periodic refresh and waits for background and lifecycle-triggered snapshot loads to finish.
    /// </summary>
    /// <returns>A task that completes after cleanup finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        _refreshCancellation.Cancel();

        try
        {
            if (_refreshTask is not null)
            {
                await _refreshTask;
            }

            // Wait for any lifecycle-triggered load to finish as well.
            await _refreshGate.WaitAsync();
            _refreshGate.Release();
        }
        finally
        {
            _refreshCancellation.Dispose();
        }
    }
}