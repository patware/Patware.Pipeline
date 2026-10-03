using Microsoft.JSInterop;

namespace Pipeline.Blazor;
// This class provides an example of how JavaScript functionality can be wrapped
// in a .NET class for easy consumption. The associated JavaScript module is
// loaded on demand when first needed.
//
// This class can be registered as scoped DI service and then injected into Blazor
// components for use.

/// <summary>
/// Wraps the sample JavaScript prompt module, importing it lazily for use by scoped Blazor services.
/// </summary>
/// <param name="jsRuntime">The Blazor JavaScript runtime used to import and invoke the sample module.</param>
public class ExampleJsInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Pipeline.Blazor/exampleJsInterop.js").AsTask());

    /// <summary>
    /// Lazily imports the sample module and opens a browser prompt with the supplied message.
    /// </summary>
    /// <param name="message">The message displayed in the browser prompt.</param>
    /// <returns>The value returned by the JavaScript prompt wrapper.</returns>
    public async ValueTask<string> Prompt(string message)
    {
        var module = await moduleTask.Value;
        return await module.InvokeAsync<string>("showPrompt", message);
    }

    /// <summary>
    /// Disposes the JavaScript module if it has been imported.
    /// </summary>
    /// <returns>A task that completes after cleanup finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
