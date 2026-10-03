/// <summary>
/// Demonstrates an asynchronous output producer by converting text to invariant uppercase.
/// </summary>
public sealed class ExampleStep
{
    /// <summary>
    /// Checks cancellation and converts the supplied text to invariant uppercase.
    /// </summary>
    /// <param name="value">The text to convert to invariant uppercase.</param>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>The uppercase text.</returns>
    public Task<string> ExecuteAsync(string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(value.ToUpperInvariant());
    }
}