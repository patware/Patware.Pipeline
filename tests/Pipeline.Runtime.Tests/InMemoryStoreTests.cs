using Pipeline.Tests.Shared;

namespace Pipeline.Runtime.Tests;

[TestClass]
public class InMemoryStoreTests : StoreContractTests
{
    [TestInitialize]
    public void Initialize()
    {
        var store = new InMemoryPipelineStore(Clock, Gate);
        Store = store; Execution = store; Outputs = store; Reset = store;
    }

    [TestCleanup]
    public void Cleanup() => Gate.Dispose();

    [TestMethod]
    public async Task Paging_is_newest_first_and_active_list_excludes_terminal_runs()
    {
        var older = Samples.Run();
        var newer = Samples.Run() with { CreatedAt = older.CreatedAt.AddMinutes(1), Status = PipelineStatus.Completed };
        await Store.CreateAsync(older, Samples.Request());
        await Store.CreateAsync(newer, Samples.Request());
        (await Store.ListAsync(0, 1)).Should().ContainSingle().Which.Id.Should().Be(newer.Id);
        (await Store.ListAsync(1, 1)).Should().ContainSingle().Which.Id.Should().Be(older.Id);
        (await Execution.ListActiveRunIdsAsync(default)).Should().Equal(older.Id);
    }
}
