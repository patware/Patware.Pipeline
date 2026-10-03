/// <summary>
/// Provides a minimal output producer that returns its input message unchanged.
/// </summary>
public sealed class NoOpStep
{
    /// <summary>
    /// Checks cancellation and returns the supplied message unchanged.
    /// </summary>
    /// <param name="message">The message returned unchanged as this step's output.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The original message.</returns>
    public Task<string> ExecuteAsync(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(message);
    }
}