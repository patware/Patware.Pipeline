using Microsoft.Extensions.DependencyInjection;

namespace Pipeline.Runtime;

/// <summary>
/// Selects persistence and processing during the AddPipeline configuration callback; becomes immutable afterward.
/// </summary>
public sealed class PipelineRegistrationOptions
{
    private bool frozen;

    internal PipelineRegistrationOptions()
    {
    }

    internal PipelinePersistenceConfiguration Persistence { get; private set; } = PipelinePersistenceConfiguration.InMemory;

    internal Action<IServiceCollection>? PersistenceRegistration
    {
        get;
        private set;
    }

    internal Action<
        IServiceCollection,
        PipelinePersistenceConfiguration>? ProcessorRegistration
    {
        get;
        private set;
    }

    /// <summary>
    /// Selects persistence and its service-registration callback during pipeline configuration.
    /// </summary>
    /// <param name="configuration">The persistence configuration shared with the processor.</param>
    /// <param name="registerServices">The callback that registers the selected provider's services.</param>
    /// <returns>This options instance. A later selection within the callback replaces the earlier selection.</returns>
    /// <exception cref="InvalidOperationException">The configuration callback has already completed.</exception>
    public PipelineRegistrationOptions SelectPersistence(
        PipelinePersistenceConfiguration configuration,
        Action<IServiceCollection> registerServices)
    {
        EnsureMutable();

        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(registerServices);

        Persistence = configuration;
        PersistenceRegistration = registerServices;

        return this;
    }

    /// <summary>
    /// Selects the execution processor's service-registration callback during pipeline configuration.
    /// </summary>
    /// <param name="registerServices">The callback that registers the selected provider's services.</param>
    /// <returns>This options instance. A later selection within the callback replaces the earlier selection.</returns>
    /// <exception cref="InvalidOperationException">The configuration callback has already completed.</exception>
    public PipelineRegistrationOptions SelectProcessor(
        Action<IServiceCollection, PipelinePersistenceConfiguration>
            registerServices)
    {
        EnsureMutable();

        ArgumentNullException.ThrowIfNull(registerServices);

        ProcessorRegistration = registerServices;

        return this;
    }

    internal void Freeze()
    {
        frozen = true;
    }

    private void EnsureMutable()
    {
        if (frozen)
        {
            throw new InvalidOperationException(
                "Pipeline configuration cannot be changed after " +
                "AddPipeline has completed its configuration callback.");
        }
    }
}