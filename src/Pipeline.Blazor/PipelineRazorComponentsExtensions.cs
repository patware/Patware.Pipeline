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
    /// Configures endpoint discovery for pipeline pages.
    /// Use PipelineRouter for automatic component-router discovery,
    /// or include the pipeline assembly in the host Router's AdditionalAssemblies.
    /// </remarks>
    public static RazorComponentsEndpointConventionBuilder AddPipelinePages(this RazorComponentsEndpointConventionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddAdditionalAssemblies(typeof(LivePipelinePage).Assembly);
    }
}
