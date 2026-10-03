using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pipeline.Runtime;

namespace Pipeline.Persistence.EntityFrameworkCore.Tests;

[TestClass]
public class RegistrationTests
{
    [TestMethod]
    public void Sql_registration_configures_provider_services_and_processor_persistence()
    {
        const string connection = "Server=(localdb)\\MSSQLLocalDB;Database=PipelineTests;Integrated Security=true";
        var services = new ServiceCollection().AddLogging();
        PipelinePersistenceConfiguration? selected = null;
        services.AddPipeline(options => options.UseSqlServer(connection, sql => sql.CommandTimeout(42))
            .SelectProcessor((_, persistence) => selected = persistence));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        provider.GetRequiredService<IPipelineStore>().Should().BeOfType<EfPipelineStore>();
        provider.GetRequiredService<IPipelineExecutionStore>().Should().BeOfType<EfPipelineExecutionStore>();
        scope.ServiceProvider.GetRequiredService<IStepOutputReader>().Should().BeOfType<EfStepOutputReader>();
        provider.GetRequiredService<IPipelineResetService>().Should().BeSameAs(provider.GetRequiredService<EfPipelineResetService>());
        selected!.Kind.Should().Be(PipelinePersistenceKind.SqlServer);
        selected.ConnectionString.Should().Be(connection);
        using var context = provider.GetRequiredService<IDbContextFactory<PipelineDbContext>>().CreateDbContext();
        context.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
        context.Database.GetCommandTimeout().Should().Be(42);
    }

    [TestMethod]
    [DataRow(null)] [DataRow("")] [DataRow(" ")]
    public void Sql_registration_rejects_missing_connection(string? connection)
    {
        Action register = () => new ServiceCollection().AddPipeline(options => options.UseSqlServer(connection!));
        register.Should().Throw<ArgumentException>();
    }
}
