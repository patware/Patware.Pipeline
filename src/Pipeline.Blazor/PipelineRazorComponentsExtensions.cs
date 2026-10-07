using Microsoft.AspNetCore.Builder;

using Pipeline.Blazor.Pages;

namespace Pipeline.Blazor;

/// <summary>
/// Adds pipeline pages to a host's Razor component endpoints.
/// </summary>
public static class PipelineRazorComponentsExtensions
{
    /// <summary>
    /// Registers the assembly containing pipeline pages for endpoint discovery.
    /// </summary>
    /// <param name="builder">
    /// The builder configuring the host's Razor component endpoints.
    /// </param>
    /// <returns>
    /// The same builder so additional component configuration can be chained.
    /// </returns>
    /// <remarks>
    /// The host's interactive Router must also discover the pipeline assembly.
    /// </remarks>
    public static RazorComponentsEndpointConventionBuilder AddPipelinePages(this RazorComponentsEndpointConventionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddAdditionalAssemblies(typeof(LivePipelinePage).Assembly);
    }
}