using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Pipeline.Contracts;
using Pipeline.Core;
using Pipeline.Core.Pipelines;
using Pipeline.Core.Steps;
using Pipeline.Runtime.Hosting;
using Pipeline.Runtime.Pipelines;

namespace Pipeline.Runtime;

/// <summary>
/// Registers shared pipeline services and the chosen persistence and execution providers.
/// </summary>
public static class PipelineServiceCollectionExtensions
{
    /// <remarks>
    /// Configures endpoint discovery for pipeline pages.
    /// Use PipelineRouter for automatic component-router discovery,
    /// or include the pipeline assembly in the host Router's
    /// AdditionalAssemblies parameter.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddPipeline();
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

        if (options.RunLogFormattingDemoOnStartup)
        {
            services.AddHostedService<LogFormattingDemoStartupService>();
        }

        services.AddSingleton(new PipelineRegistrationMarker());

        return services;
    }

    private static void RegisterSharedServices(IServiceCollection services)
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

        RegisterBuiltInDefinitions(services);
    }

    private static void RegisterInMemoryPersistence(IServiceCollection services)
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

    private static void RegisterBuiltInDefinitions(IServiceCollection services)
    {
        services.TryAddTransient<global::NoOpStep>();
        services.TryAddTransient<global::ExampleStep>();

        services.TryAddTransient<LogFormattingStep>();
        services.TryAddTransient<LogFormattingPipeline>();

        services.TryAddEnumerable(
            ServiceDescriptor.Transient<
                IPipelineDefinitionRegistration,
                LogFormattingRegistration>());
    }

    private sealed class PipelineRegistrationMarker
    {
    }
}