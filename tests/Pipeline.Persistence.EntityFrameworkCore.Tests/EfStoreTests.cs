using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pipeline.Tests.Shared;

namespace Pipeline.Persistence.EntityFrameworkCore.Tests;

[TestClass]
public class EfStoreTests : StoreContractTests
{
    private SqliteConnection connection = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PipelineDbContext>().UseSqlite(connection).Options;
        var factory = Substitute.For<IDbContextFactory<PipelineDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(new PipelineDbContext(options)));
        await using var context = new PipelineDbContext(options);
        await context.Database.EnsureCreatedAsync();
        Store = new EfPipelineStore(factory);
        Execution = new EfPipelineExecutionStore(factory, Clock);
        Outputs = new EfStepOutputReader(factory);
        Reset = new EfPipelineResetService(factory, Gate);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await connection.DisposeAsync();
        Gate.Dispose();
    }
}
