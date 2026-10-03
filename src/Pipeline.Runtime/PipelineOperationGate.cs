namespace Pipeline.Runtime;

/// <summary>
/// Serializes pipeline operations within this process, coordinating execution, submission, retry, and reset.
/// </summary>
public sealed class PipelineOperationGate : IDisposable
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    /// <summary>
    /// Runs an asynchronous operation exclusively within this process, releasing the gate even when it fails.
    /// </summary>
    /// <typeparam name="T">The result type returned by the operation.</typeparam>
    /// <param name="operation">The asynchronous operation to run while holding the process-local gate.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <remarks>The gate is process-local and non-reentrant. Do not acquire it recursively; database revisions and leases coordinate separate processes.</remarks>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await operation();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Releases the gate's semaphore after operations have stopped.
    /// </summary>
    public void Dispose()
    {
        semaphore.Dispose();
    }
}