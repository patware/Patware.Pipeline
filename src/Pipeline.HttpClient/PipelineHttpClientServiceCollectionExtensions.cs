using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;

using Pipeline.Contracts;

namespace Pipeline.HttpClient;

/// <summary>
/// Registers remote pipeline monitoring services.
/// </summary>
public static class PipelineHttpClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers an HTTP implementation of IPipelineMonitor.
    /// </summary>
    /// <param name="services">
    /// The service collection receiving the registration.
    /// </param>
    /// <param name="baseAddress">
    /// The absolute address of the backend application.
    /// This may include an application path prefix.
    /// </param>
    /// <returns>
    /// The client builder for further authentication, handler,
    /// and HTTP configuration.
    /// </returns>
    /// <remarks>
    /// Use this registration in a renderer host that connects to a
    /// separate pipeline backend.
    /// It registers monitoring and the display clock without registering
    /// an executor.
    /// Register host-wide HTTP defaults before calling this method.
    /// Inherited resilience handlers are replaced to prevent automatic
    /// replay of commands while retaining other handlers, such as
    /// service discovery.
    /// Relative request paths preserve any application prefix in the
    /// base address.
    /// Removing inherited resilience handlers currently requires an
    /// experimental Microsoft API. Its diagnostic is suppressed only
    /// at that call. This prevents an inherited retry policy from
    /// replaying commands before the client-specific policy takes effect.
    /// Review this dependency when upgrading the resilience package.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// The service collection or base address is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The address is relative or contains a query or fragment.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A pipeline monitor has already been registered.
    /// </exception>
    public static IHttpClientBuilder AddPipelineClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        if (!baseAddress.IsAbsoluteUri ||
            !string.IsNullOrEmpty(baseAddress.Query) ||
            !string.IsNullOrEmpty(baseAddress.Fragment))
        {
            throw new ArgumentException(
                "The backend address must be absolute and contain " +
                "no query or fragment.",
                nameof(baseAddress));
        }

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(IPipelineMonitor)))
        {
            throw new InvalidOperationException(
                "A pipeline monitor is already registered. " +
                "Configure one monitoring source per host.");
        }

        var address = baseAddress.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
                ? baseAddress
                : new Uri(baseAddress.AbsoluteUri + "/");

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        var clientBuilder =
            services.AddHttpClient<IPipelineMonitor, HttpPipelineMonitor>(
                client =>
                {
                    client.BaseAddress = address;
                });

        // Replace inherited resilience policies so retry commands cannot
        // be replayed by a host-wide policy.
#pragma warning disable EXTEXP0001 // Required to replace inherited resilience handlers.
        clientBuilder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        clientBuilder.AddStandardResilienceHandler(options =>
        {
            options.Retry.DisableForUnsafeHttpMethods();
        });
        return clientBuilder;
    }
}