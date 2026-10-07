using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Pipeline.Contracts;
using Pipeline.Core;

namespace Pipeline.Runtime;

/// <summary>
/// Registers shared pipeline services and the chosen persistence and execution providers.
/// </summary>
public static class PipelineServiceCollectionExtensions
{
    /// <summary>
    /// Registers pipeline services once, defaulting to in-memory persistence and the built-in hosted worker.
    /// </summary>
    /// <param name="services">The service collection receiving pipeline registrations.</param>
    /// <param name="configure">The optional callback selecting persistence and processing before registration is frozen.</param>
    /// <returns>The service collection for further registration.</returns>
    /// <exception cref="InvalidOperationException">Pipeline services have already been registered.</exception>
    /// <remarks>
    /// Register step service types and <see cref="IPipelineDefinitionRegistration" /> implementations separately.
    /// Every persisted definition ID and version must remain registered so queued and recovered runs can restore their plans.
    /// Persistence and processing are selected in the same callback and the options are frozen when it returns.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddPipeline();
    /// services.AddTransient&lt;Pipeline.Core.Steps.LogFormattingStep&gt;();
    /// services.AddTransient&lt;Pipeline.Core.Pipelines.LogFormattingPipeline&gt;();
    /// services.AddTransient&lt;IPipelineDefinitionRegistration, Pipeline.Runtime.Pipelines.LogFormattingRegistration&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddPipeline(
        this IServiceCollection services,
        Action<PipelineRegistrationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor =>
            descriptor.ServiceType == typeof(PipelineRegistrationMarker)))
        {
            throw new InvalidOperationException(
                "AddPipeline must be called once. Configure persistence " +
                "and processing in the same callback.");
        }

        var options = new PipelineRegistrationOptions();

        configure?.Invoke(options);
        options.Freeze();

        RegisterSharedServices(services);

        services.AddSingleton(options.Persistence);

        if (options.PersistenceRegistration is { } registerPersistence)
        {
            registerPersistence(services);
        }
        else
        {
            RegisterInMemoryPersistence(services);
        }

        if (options.ProcessorRegistration is { } registerProcessor)
        {
            registerProcessor(services, options.Persistence);
        }
        else
        {
            RegisterBuiltInProcessor(services);
        }

        services.AddSingleton(new PipelineRegistrationMarker());

        return services;
    }

    private static void RegisterSharedServices(
        IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        services.TryAddSingleton<IPipelineBuilderFactory, PipelineBuilderFactory>();

        services.TryAddSingleton<PipelineOperationGate>();

        // Both processors use this service for submission and queries.
        services.TryAddSingleton<PipelineRuntime>();

        services.TryAddSingleton<PipelineRunEventQueue>();

        services.TryAddScoped<IPipelineDefinitionRegistry, PipelineDefinitionRegistry>();

        services.TryAddScoped<IStepArgumentBinder, StepArgumentBinder>();

        services.TryAddScoped<IStepInvoker, StepInvoker>();

        services.TryAddScoped<PipelineExecutionCoordinator>();

        services.TryAddScoped<PipelineStepLogger>();

        services.TryAddScoped<IPipelineStepLogger>(provider => provider.GetRequiredService<PipelineStepLogger>());

        services.TryAddScoped<PipelineStepContext>();

        services.TryAddScoped<IPipelineStepContext>(provider => provider.GetRequiredService<PipelineStepContext>());

        services.TryAddScoped<PipelineRetryService>();

        services.TryAddScoped<IPipelineMonitor, LocalPipelineMonitor>();

        services.AddHostedService<PipelineRunEventDispatcher>();
    }

    private static void RegisterInMemoryPersistence(
        IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryPipelineStore>();

        services.AddSingleton<IPipelineStore>(provider =>
            provider.GetRequiredService<InMemoryPipelineStore>());

        services.AddSingleton<IPipelineExecutionStore>(provider =>
            provider.GetRequiredService<InMemoryPipelineStore>());

        services.AddSingleton<IStepOutputReader>(provider =>
            provider.GetRequiredService<InMemoryPipelineStore>());

        services.AddSingleton<IPipelineResetService>(provider =>
            provider.GetRequiredService<InMemoryPipelineStore>());
    }

    private static void RegisterBuiltInProcessor(IServiceCollection services)
    {
        services.AddSingleton<IPipelineRuntime>(provider =>
            provider.GetRequiredService<PipelineRuntime>());

        services.AddHostedService<PipelineWorker>();
    }

    private sealed class PipelineRegistrationMarker
    {
    }
}