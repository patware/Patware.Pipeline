using Pipeline.Core;
using Pipeline.Runtime;

namespace Pipeline.Tests.Shared;

// Executed by both concrete fixtures to keep the persistence implementations honest.
public abstract class StoreContractTests
{
    protected IPipelineStore Store = null!;
    protected IPipelineExecutionStore Execution = null!;
    protected IStepOutputReader Outputs = null!;
    protected IPipelineResetService Reset = null!;
    protected readonly TestClock Clock = new();
    protected readonly PipelineOperationGate Gate = new();

    private async Task<PipelineExecutionSnapshot> Seed()
    {
        var run = Samples.Run();
        await Store.CreateAsync(run, Samples.Request());
        return (await Execution.LoadAsync(run.Id, default))!;
    }

    [TestMethod]
    public async Task Create_roundtrips_metadata_specification_and_pending_graph()
    {
        var snapshot = await Seed();
        (await Store.GetAsync(snapshot.Run.Id)).Should().BeEquivalentTo(snapshot.Run);
        var spec = await Store.GetSpecificationAsync(snapshot.Run.Id);
        spec.Should().Be(new PipelineExecutionSpecification("test", 2, "{\"Value\":1}", "{\"Enabled\":true}"));
        snapshot.Jobs.Should().ContainSingle().Which.Status.Should().Be(JobExecutionStatus.Pending);
        snapshot.Steps.Should().ContainSingle().Which.Status.Should().Be(StepExecutionStatus.Pending);
        (await Outputs.ReadAsync(snapshot.Run.Id, "job", "step", default)).Should().Be(new StoredStepOutput(false, false, null));
    }

    [TestMethod]
    public async Task Missing_run_and_output_return_null()
    {
        var id = Guid.NewGuid();
        (await Store.GetAsync(id)).Should().BeNull();
        (await Store.GetSpecificationAsync(id)).Should().BeNull();
        (await Execution.LoadAsync(id, default)).Should().BeNull();
        (await Outputs.ReadAsync(id, "job", "step", default)).Should().BeNull();
    }

    [TestMethod]
    public async Task Update_uses_optimistic_revision_and_appends_logs()
    {
        var snapshot = await Seed();
        var updated = snapshot.Run with { Revision = 1, Status = PipelineStatus.Running,
            Logs = [.. snapshot.Run.Logs, new(Clock.Now, "Started") { Level = PipelineLogLevel.Warning, JobId = "job", StepId = "step" }] };
        (await Store.TryUpdateAsync(updated, 0)).Should().BeTrue();
        (await Store.TryUpdateAsync(updated, 0)).Should().BeFalse();
        (await Store.GetAsync(updated.Id)).Should().BeEquivalentTo(updated);
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task Update_rejects_history_changes_and_rolls_back(bool remove)
    {
        var snapshot = await Seed();
        var replacement = snapshot.Run with { Revision = 1, Logs = remove ? [] : [new(Clock.Now, "Tampered")] };
        Func<Task> update = () => Store.TryUpdateAsync(replacement, 0);
        await update.Should().ThrowAsync<ArgumentException>();
        (await Store.GetAsync(snapshot.Run.Id)).Should().BeEquivalentTo(snapshot.Run);
    }

    [TestMethod]
    public async Task Save_advances_all_revisions_preserves_history_and_publishes_output()
    {
        var snapshot = await Seed();
        var replacement = snapshot with
        {
            Run = snapshot.Run with { Status = PipelineStatus.Completed, Logs = [], Revision = 999 },
            Jobs = [snapshot.Jobs[0] with { Status = JobExecutionStatus.Succeeded, Revision = 999 }],
            Steps = [snapshot.Steps[0] with { Status = StepExecutionStatus.Succeeded, Revision = 999, HasOutput = true, OutputJson = "\"result\"", ArgumentsJson = "[\"hello\"]", Attempt = 1 }]
        };
        (await Execution.TrySaveAsync(replacement, 0, "Done", null, default, PipelineLogLevel.Warning, "job", "step")).Should().BeTrue();
        (await Execution.TrySaveAsync(replacement, 0, "Stale", null, default)).Should().BeFalse();
        var saved = (await Execution.LoadAsync(snapshot.Run.Id, default))!;
        saved.Run.Revision.Should().Be(1);
        saved.Jobs[0].Revision.Should().Be(1);
        saved.Steps[0].Revision.Should().Be(1);
        saved.Steps[0].ArgumentsJson.Should().Be("[\"hello\"]");
        saved.Run.Logs.Select(x => x.Message).Should().Equal("Queued", "Done");
        saved.Run.Logs[1].Should().Be(new PipelineLogEntry(Clock.Now, "Done") { Level = PipelineLogLevel.Warning, JobId = "job", StepId = "step" });
        (await Outputs.ReadAsync(snapshot.Run.Id, "job", "step", default)).Should().Be(new StoredStepOutput(true, true, "\"result\""));
    }

    [TestMethod]
    [DataRow("missing", null)] [DataRow(null, "step")] [DataRow("job", "missing")] [DataRow(" ", null)]
    public async Task Save_rejects_invalid_log_scope(string? job, string? step)
    {
        var snapshot = await Seed();
        Func<Task> save = () => Execution.TrySaveAsync(snapshot, 0, "message", null, default, jobId: job, stepId: step);
        await save.Should().ThrowAsync<ArgumentException>();
        (await Store.GetAsync(snapshot.Run.Id))!.Revision.Should().Be(0);
    }

    [TestMethod]
    public async Task Save_rejects_changed_graph_without_partial_update()
    {
        var snapshot = await Seed();
        Func<Task> save = () => Execution.TrySaveAsync(snapshot with { Steps = [] }, 0, "bad", null, default);
        await save.Should().ThrowAsync<InvalidOperationException>();
        (await Execution.LoadAsync(snapshot.Run.Id, default)).Should().BeEquivalentTo(snapshot);
    }

    [TestMethod]
    [DataRow(false, false)] [DataRow(true, false)] [DataRow(false, true)]
    public async Task Claim_fences_wrong_owner_and_expired_lease(bool wrongToken, bool expired)
    {
        var snapshot = await Seed();
        var token = Guid.NewGuid();
        var until = Clock.Now.AddMinutes(1);
        var running = snapshot with { Steps = [snapshot.Steps[0] with { Status = StepExecutionStatus.Running, LeaseToken = token, LeaseExpiresAt = until }] };
        (await Execution.TrySaveAsync(running, 0, "Claimed", null, default)).Should().BeTrue();
        running = (await Execution.LoadAsync(snapshot.Run.Id, default))!;
        if (expired) Clock.Now = until;
        var claim = new StepClaim(snapshot.Run.Id, "job", "step", wrongToken ? Guid.NewGuid() : token, until);
        (await Execution.TrySaveAsync(running, 1, "Owned", claim, default, jobId: "job", stepId: "step")).Should().Be(!wrongToken && !expired);
        (await Store.GetAsync(snapshot.Run.Id))!.Revision.Should().Be(!wrongToken && !expired ? 2 : 1);
    }

    [TestMethod]
    public async Task Reset_deletes_entire_graph_and_returns_run_count()
    {
        var snapshot = await Seed();
        await Seed();
        (await Reset.ResetAsync()).Should().Be(2);
        (await Store.GetAsync(snapshot.Run.Id)).Should().BeNull();
        (await Store.GetSpecificationAsync(snapshot.Run.Id)).Should().BeNull();
        (await Execution.LoadAsync(snapshot.Run.Id, default)).Should().BeNull();
        (await Outputs.ReadAsync(snapshot.Run.Id, "job", "step", default)).Should().BeNull();
        (await Reset.ResetAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task Create_rejects_invalid_revision_and_mismatched_definition()
    {
        var run = Samples.Run();
        Func<Task> revision = () => Store.CreateAsync(run with { Revision = 1 }, Samples.Request());
        Func<Task> mismatch = () => Store.CreateAsync(run with { Definition = new("other", "Other") }, Samples.Request());
        await revision.Should().ThrowAsync<ArgumentException>();
        await mismatch.Should().ThrowAsync<ArgumentException>();
        (await Store.GetAsync(run.Id)).Should().BeNull();
    }

    [TestMethod]
    [DataRow(-1, 1)] [DataRow(0, 0)] [DataRow(0, -1)]
    public async Task Paging_rejects_invalid_bounds(int skip, int take)
    {
        Func<Task> list = () => Store.ListAsync(skip, take);
        await list.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public async Task Cancelled_create_does_not_persist()
    {
        var run = Samples.Run();
        Func<Task> create = () => Store.CreateAsync(run, Samples.Request(), new CancellationToken(true));
        await create.Should().ThrowAsync<OperationCanceledException>();
        (await Store.GetAsync(run.Id)).Should().BeNull();
    }
}

