using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Registers SQL Server pipeline persistence through an Entity Framework Core context factory.
/// </summary>
public static class PipelineEntityFrameworkCoreExtensions
{
    /// <summary>
    /// Selects SQL Server persistence and registers stores and a context factory; schema creation or migration is the host's responsibility.
    /// </summary>
    /// <param name="options">The pipeline registration options or typed database context options to use.</param>
    /// <param name="connectionString">The SQL Server connection string, or null for in-memory persistence.</param>
    /// <param name="configureSqlServer">The optional callback customizing SQL Server provider options.</param>
    /// <returns>The pipeline options for further configuration.</returns>
    /// <remarks>Call within <see cref="PipelineServiceCollectionExtensions.AddPipeline" />. Apply migrations before processing runs; this registration does not create the schema.</remarks>
    public static PipelineRegistrationOptions UseSqlServer(
        this PipelineRegistrationOptions options,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder>? configureSqlServer = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var persistence = PipelinePersistenceConfiguration.SqlServer(connectionString);

        return options.SelectPersistence(persistence, services =>
        {
            services.AddDbContextFactory<PipelineDbContext>(db =>
            {
                db.UseSqlServer(connectionString, configureSqlServer);
            });

            services.AddSingleton<IPipelineStore, EfPipelineStore>();

            services.AddSingleton<
                IPipelineExecutionStore,
                EfPipelineExecutionStore>();

            services.AddScoped<IStepOutputReader, EfStepOutputReader>();

            services.AddSingleton<EfPipelineResetService>();

            services.AddSingleton<IPipelineResetService>(provider =>
                provider.GetRequiredService<EfPipelineResetService>());
        });
    }
}