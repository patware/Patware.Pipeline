using System.Linq.Expressions;
using Pipeline.Core;

namespace Pipeline.Core.Tests;

[TestClass]
public class GraphBuilderTests
{
    private static IPipelineBuilder Builder() => new PipelineBuilderFactory().Create(new("sample", "Sample"));
    public interface ISteps
    {
        Task Run(string value, CancellationToken token);
        Task<string> Produce(string value, CancellationToken token);
        Task<string> Combine(string first, string second, CancellationToken token);
        Task<bool> Check(string value, CancellationToken token);
    }

    [TestMethod]
    public void Build_preserves_order_bindings_dependencies_and_poll_settings()
    {
        var builder = Builder();
        var first = builder.AddJob("first");
        var a = first.Step<ISteps>("a").Produces((s, ct) => s.Produce("a", ct));
        var b = first.Step<ISteps>("b").Using(a).Produces((s, x, ct) => s.Produce(x, ct));
        first.Step<ISteps>("combine").Using(a, b).Produces((s, x, y, ct) => s.Combine(x, y, ct));
        var second = builder.AddJob("second").After(first, true).When(b, x => x.Length > 0);
        second.Step<ISteps>("command").Using(a, b).Execute((s, x, y, ct) => s.Run(x, ct));
        second.Poll<ISteps>("poll").Using(b).Check((s, x, ct) => s.Check(x, ct), TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1));
        var plan = builder.Build();
        plan.Jobs.Select(x => x.Id).Should().Equal("first", "second");
        plan.Jobs[0].Steps.Select(x => x.Id).Should().Equal("a", "b", "combine");
        plan.Jobs[1].Dependencies.Should().Equal(new JobDependency("first", true));
        plan.Jobs[1].Condition!.Source.Should().Be(new OutputReference("first", "b", typeof(string)));
        plan.Jobs[1].Steps[0].Inputs.Select(x => x.StepId).Should().Equal("a", "b");
        plan.Jobs[1].Steps[1].Poll.Should().Be(new PollSettings(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1)));
    }

    [TestMethod]
    public void Build_rejects_empty_pipeline_empty_job_and_incomplete_step()
    {
        var builder = Builder();
        builder.Invoking(x => x.Build()).Should().Throw<InvalidOperationException>().WithMessage("*at least one job*");
        var job = builder.AddJob("job");
        builder.Invoking(x => x.Build()).Should().Throw<InvalidOperationException>().WithMessage("*at least one step*");
        job.Step<ISteps>("step");
        builder.Invoking(x => x.Build()).Should().Throw<InvalidOperationException>().WithMessage("*incomplete*");
    }

    [TestMethod]
    public void Build_freezes_all_builder_handles()
    {
        var builder = Builder();
        var job = builder.AddJob("job");
        var step = job.Step<ISteps>("step");
        step.Execute((s, ct) => s.Run("value", ct));
        builder.Build();
        builder.Invoking(x => x.Build()).Should().Throw<InvalidOperationException>();
        builder.Invoking(x => x.AddJob("other")).Should().Throw<InvalidOperationException>();
        job.Invoking(x => x.Step<ISteps>("other")).Should().Throw<InvalidOperationException>();
        step.Invoking(x => x.Execute((s, ct) => s.Run("again", ct))).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Duplicate_jobs_steps_dependencies_conditions_and_completions_are_rejected()
    {
        var builder = Builder();
        var a = builder.AddJob("a");
        builder.Invoking(x => x.AddJob("a")).Should().Throw<InvalidOperationException>();
        var output = a.Step<ISteps>("produce").Produces((s, ct) => s.Produce("x", ct));
        a.Invoking(x => x.Poll<ISteps>("produce")).Should().Throw<InvalidOperationException>();
        var b = builder.AddJob("b").After(a).When(output, x => x.Length > 0);
        b.Invoking(x => x.After(a)).Should().Throw<InvalidOperationException>();
        b.Invoking(x => x.When(output, y => y.Length > 0)).Should().Throw<InvalidOperationException>();
        var step = b.Step<ISteps>("run");
        step.Execute((s, ct) => s.Run("x", ct));
        step.Invoking(x => x.Execute((s, ct) => s.Run("x", ct))).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Cycles_and_foreign_dependencies_are_rejected()
    {
        var builder = Builder();
        var a = builder.AddJob("a");
        var b = builder.AddJob("b").After(a);
        a.After(b);
        builder.Invoking(x => x.Build()).Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
        a.Invoking(x => x.After(Builder().AddJob("foreign"))).Should().Throw<InvalidOperationException>();
        a.Invoking(x => x.After(Substitute.For<IJobBuilder>())).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Outputs_require_same_builder_and_ancestor_or_earlier_step()
    {
        var builder = Builder();
        var a = builder.AddJob("a");
        var consumer = a.Step<ISteps>("early");
        var output = a.Step<ISteps>("late").Produces((s, ct) => s.Produce("x", ct));
        consumer.Using(output).Execute((s, x, ct) => s.Run(x, ct));
        builder.Invoking(x => x.Build()).Should().Throw<InvalidOperationException>().WithMessage("*before*");
        var other = Builder().AddJob("other");
        other.Step<ISteps>("run").Invoking(x => x.Using(output)).Should().Throw<InvalidOperationException>().WithMessage("*another*");
        var independent = Builder();
        var producer = independent.AddJob("p").Step<ISteps>("p").Produces((s, ct) => s.Produce("x", ct));
        independent.AddJob("c").Step<ISteps>("c").Using(producer).Execute((s, x, ct) => s.Run(x, ct));
        independent.Invoking(x => x.Build()).Should().Throw<InvalidOperationException>().WithMessage("*must depend*");
    }

    [TestMethod]
    public void Transitive_ancestor_output_is_available()
    {
        var builder = Builder();
        var a = builder.AddJob("a");
        var output = a.Step<ISteps>("p").Produces((s, ct) => s.Produce("x", ct));
        var b = builder.AddJob("b").After(a);
        b.Step<ISteps>("b").Execute((s, ct) => s.Run("x", ct));
        builder.AddJob("c").After(b).Step<ISteps>("c").Using(output).Execute((s, x, ct) => s.Run(x, ct));
        builder.Build().Jobs.Should().HaveCount(3);
    }

    [TestMethod]
    [DataRow(0, 1)] [DataRow(-1, 1)] [DataRow(1, 0)] [DataRow(1, -1)]
    public void Poll_requires_positive_interval_and_timeout(int every, int timeout)
    {
        var poll = Builder().AddJob("j").Poll<ISteps>("p");
        poll.Invoking(x => x.Check((s, ct) => s.Check("x", ct), TimeSpan.FromSeconds(every), TimeSpan.FromSeconds(timeout)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    [DataRow("")] [DataRow(" ")] [DataRow(null)]
    public void Blank_identifiers_are_rejected(string? id)
    {
        var builder = Builder();
        builder.Invoking(x => x.AddJob(id!)).Should().Throw<ArgumentException>();
        builder.AddJob("j").Invoking(x => x.Step<ISteps>(id!)).Should().Throw<ArgumentException>();
        new PipelineBuilderFactory().Invoking(x => x.Create(new(id!, "name"))).Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Factory_validates_definition_and_returns_independent_builders()
    {
        var factory = new PipelineBuilderFactory();
        factory.Invoking(x => x.Create(null!)).Should().Throw<ArgumentNullException>();
        factory.Invoking(x => x.Create(new("id", " "))).Should().Throw<ArgumentException>();
        factory.Invoking(x => x.Create(new("id", "name", 0))).Should().Throw<ArgumentOutOfRangeException>();
        factory.Create(new("id", "name")).Should().NotBeSameAs(factory.Create(new("id", "name")));
    }

    [TestMethod]
    public void Invocation_requires_direct_service_call_and_original_cancellation_token()
    {
        var job = Builder().AddJob("j");
        var service = Substitute.For<ISteps>();
        job.Step<ISteps>("foreign").Invoking(x => x.Execute((s, ct) => service.Run("x", ct))).Should().Throw<InvalidOperationException>();
        job.Step<ISteps>("token").Invoking(x => x.Execute((s, ct) => s.Run("x", CancellationToken.None))).Should().Throw<InvalidOperationException>();
        job.Step<ISteps>("task").Invoking(x => x.Execute((s, ct) => Task.CompletedTask)).Should().Throw<InvalidOperationException>();
        job.Step<ISteps>("result").Invoking(x => x.Execute((s, ct) => s.Produce("x", ct))).Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Request_serializes_payload_and_rejects_mismatched_definition()
    {
        var builder = Builder();
        builder.AddJob("j").Step<ISteps>("s").Execute((s, ct) => s.Run("x", ct));
        var plan = builder.Build();
        var request = PipelineRequest.Create(plan.Definition, "title", "user", new { Value = 42 }, new { Enabled = true }, plan);
        request.InputJson.Should().Be("{\"Value\":42}");
        request.SettingsJson.Should().Be("{\"Enabled\":true}");
        request.Plan.Should().BeSameAs(plan);
        Action mismatch = () => PipelineRequest.Create(plan.Definition with { Version = 2 }, "title", "user", 1, 2, plan);
        mismatch.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Expression_validator_accepts_supported_predicates_and_rejects_computation()
    {
        var minimum = 3;
        Expression<Func<string, bool>> supported = x => !(x.Length < minimum) && (x.Length == 4 || x.Length >= 5);
        Action valid = () => ArgumentExpressionValidator.ValidateCondition(supported);
        valid.Should().NotThrow();
        Expression<Func<string, bool>> method = x => x.StartsWith("a");
        Expression<Func<int, bool>> arithmetic = x => x + 1 > 0;
        Expression<Func<int, bool>> statics = x => x > DateTime.Now.Day;
        foreach (var expression in new LambdaExpression[] { method, arithmetic, statics })
        {
            Action invalid = () => ArgumentExpressionValidator.ValidateCondition(expression);
            invalid.Should().Throw<InvalidOperationException>().WithMessage("*not supported*");
        }
    }
}
