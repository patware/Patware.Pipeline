using Pipeline.Core;
using Pipeline.Runtime;

namespace Pipeline.Tests.Shared;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public interface ITestStep
{
    Task<string> Produce(string value, CancellationToken cancellationToken);
    Task Execute(string value, CancellationToken cancellationToken);
    Task<bool> Poll(CancellationToken cancellationToken);
}

public static class Samples
{
    public static PipelinePlan Plan()
    {
        var builder = new PipelineBuilderFactory().Create(new("test", "Test", 2));
        builder.AddJob("job").Step<ITestStep>("step").Produces((s, ct) => s.Produce("hello", ct));
        return builder.Build();
    }

    public static PipelineRequest Request(PipelinePlan? plan = null)
    {
        plan ??= Plan();
        return PipelineRequest.Create(plan.Definition, "Test run", "tester", new { Value = 1 }, new { Enabled = true }, plan);
    }

    public static PipelineRun Run(PipelinePlan? plan = null) => new()
    {
        Id = Guid.NewGuid(), Definition = (plan ?? Plan()).Definition,
        Title = "Test run", CreatedBy = "tester", CreatedAt = new TestClock().Now,
        QueuedAt = new TestClock().Now, Status = PipelineStatus.Queued,
        Logs = [new(new TestClock().Now, "Queued")]
    };
}


