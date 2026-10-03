using global::Hangfire;
using global::Hangfire.SqlServer;

using Microsoft.Extensions.DependencyInjection;

using Pipeline.Runtime;

namespace Pipeline.Hangfire;

/// <summary>
/// Selects Hangfire processing and configures its storage to match pipeline persistence.
/// </summary>
public static class PipelineHangfireExtensions
{
    /// <summary>
    /// Selects Hangfire processing, matching its storage to the selected pipeline persistence and registering startup recovery.
    /// </summary>
    /// <param name="options">The pipeline registration options or typed database context options to use.</param>
    /// <returns>The pipeline options for further configuration.</returns>
    /// <remarks>
    /// Call within <see cref="PipelineServiceCollectionExtensions.AddPipeline" />. The selected persistence configuration determines whether Hangfire uses in-memory or SQL Server storage.
    /// Registers Hangfire workers and recovery rather than <see cref="PipelineWorker" />.
    /// </remarks>
    public static PipelineRegistrationOptions UseHangfire(
        this PipelineRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.SelectProcessor(RegisterHangfire);
    }

    private static void RegisterHangfire(
        IServiceCollection services,
        PipelinePersistenceConfiguration persistence)
    {
        services.AddHangfire(configuration =>
        {
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

            switch (persistence.Kind)
            {
                case PipelinePersistenceKind.InMemory:
                    configuration.UseInMemoryStorage();
                    break;

                case PipelinePersistenceKind.SqlServer:
                    ArgumentException.ThrowIfNullOrWhiteSpace(
                        persistence.ConnectionString);

                    configuration.UseSqlServerStorage(
                        persistence.ConnectionString,
                        new SqlServerStorageOptions
                        {
                            SlidingInvisibilityTimeout =
                                TimeSpan.FromMinutes(5),

                            QueuePollInterval = TimeSpan.Zero
                        });
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported pipeline persistence: " +
                        $"{persistence.Kind}.");
            }
        });

        services.AddSingleton<
            IPipelineRuntime,
            HangfirePipelineRuntime>();

        services.AddScoped<PipelineRunJob>();
        services.AddScoped<PipelineStepJob>();
        services.AddScoped<PipelineRecoveryJob>();

        services.AddHangfireServer(server =>
        {
            server.WorkerCount = 4;
            server.SchedulePollingInterval = TimeSpan.FromSeconds(1);
        });

        services.AddHostedService<PipelineHangfireStartupService>();
    }
}