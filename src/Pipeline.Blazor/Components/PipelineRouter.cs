using System.Reflection;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

using Pipeline.Blazor.Pages;

namespace Pipeline.Blazor.Components;

/// <summary>
/// Wraps the Blazor router and automatically discovers pipeline pages.
/// </summary>
/// <remarks>
/// The host retains control of its application assembly, layout,
/// navigation callbacks, and not-found presentation.
/// Additional host assemblies are preserved and combined with the
/// pipeline assembly.
/// Endpoint discovery must still be configured through AddPipelinePages.
/// This component belongs to the renderer library so executor hosts
/// do not acquire a Blazor dependency.
/// </remarks>
public sealed class PipelineRouter : ComponentBase
{
    /// <summary>
    /// Gets or sets the assembly containing the host application's pages.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Assembly AppAssembly { get; set; } = default!;

    /// <summary>
    /// Gets or sets additional assemblies containing host application pages.
    /// </summary>
    [Parameter]
    public IEnumerable<Assembly> AdditionalAssemblies { get; set; } = [];

    /// <summary>
    /// Gets or sets the content used to display a matched route.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment<RouteData> Found { get; set; } = default!;

    /// <summary>
    /// Gets or sets the content displayed while navigation is in progress.
    /// </summary>
    [Parameter]
    public RenderFragment? Navigating { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked before navigation completes.
    /// </summary>
    [Parameter]
    public EventCallback<NavigationContext> OnNavigateAsync { get; set; }

    /// <summary>
    /// Gets or sets the routable component displayed for unmatched routes.
    /// </summary>
    [Parameter]
    public Type? NotFoundPage { get; set; }

    /// <summary>
    /// Renders the framework router with pipeline page discovery enabled.
    /// </summary>
    /// <param name="builder">
    /// The builder receiving the router component.
    /// </param>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var assemblies = (AdditionalAssemblies ?? [])
            .Append(typeof(LivePipelinePage).Assembly)
            .Where(assembly => assembly != AppAssembly)
            .Distinct()
            .ToArray();

        builder.OpenComponent<Router>(0);

        builder.AddAttribute(1, nameof(Router.AppAssembly), AppAssembly);

        builder.AddAttribute(
            2,
            nameof(Router.AdditionalAssemblies),
            assemblies);

        builder.AddAttribute(3, nameof(Router.Found), Found);

        builder.AddAttribute(4, nameof(Router.Navigating), Navigating);

        builder.AddAttribute(
            5,
            nameof(Router.OnNavigateAsync),
            OnNavigateAsync);

        builder.AddAttribute(6, nameof(Router.NotFoundPage), NotFoundPage);

        builder.CloseComponent();
    }
}