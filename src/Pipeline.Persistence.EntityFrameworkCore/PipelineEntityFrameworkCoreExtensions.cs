using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Registers SQL Server persistence and automatic database initialization.
/// </summary>
public static class PipelineEntityFrameworkCoreExtensions
{
    /// <summary>
    /// Selects SQL Server persistence and applies the library's pending database migrations before hosted workers start.
    /// </summary>
    /// <param name="options">The pipeline registration options.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="configureSqlServer">
    /// Optional SQL Server settings. Pipeline owns its migrations
    /// assembly and schema-version table.
    /// </param>
    /// <returns>The pipeline registration options.</returns>
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
                db.UseSqlServer(connectionString, sql =>
                {
                    configureSqlServer?.Invoke(sql);

                    sql.MigrationsAssembly(typeof(PipelineDbContext).Assembly.GetName().Name!);

                    sql.MigrationsHistoryTable("SchemaVersions", "pipeline");
                });
            });

            services.AddSingleton<IPipelineStore, EfPipelineStore>();
            services.AddSingleton<IPipelineExecutionStore, EfPipelineExecutionStore>();

            services.AddScoped<IStepOutputReader, EfStepOutputReader>();

            services.AddSingleton<EfPipelineResetService>();
            services.AddSingleton<IPipelineResetService>(provider => provider.GetRequiredService<EfPipelineResetService>());

            services.AddHostedService<PipelineDatabaseInitializer>();
        });
    }
}