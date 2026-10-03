using Pipeline.Core.Steps;

namespace Pipeline.Core.Pipelines;

/// <summary>
/// Represents the empty input contract for the log formatting demonstration.
/// </summary>
public sealed record LogFormattingInput();

/// <summary>
/// Represents the empty settings contract for the log formatting demonstration.
/// </summary>
public sealed record LogFormattingSettings();

/// <summary>
/// Builds the demonstration pipeline that writes log formatting samples.
/// </summary>
/// <param name="builders">The factory creating independent pipeline graph builders.</param>
public sealed class LogFormattingPipeline(IPipelineBuilderFactory builders)
{
    /// <summary>
    /// Gets the stable identity and version of the log formatting demonstration.
    /// </summary>
    public static PipelineDefinition Definition { get; } = new(
        Id: "LogFormatting",
        DisplayName: "Log formatting samples",
        Version: 1);

    /// <summary>
    /// Creates a log formatting demonstration request for the supplied submitter identity.
    /// </summary>
    /// <param name="createdBy">The caller-supplied identity of the person or service that submitted the run.</param>
    /// <returns>A request containing the demonstration plan and empty input and settings contracts.</returns>
    public PipelineRequest Build(string createdBy)
    {
        return PipelineRequest.Create(
            definition: Definition,
            title: "Log formatting samples",
            createdBy: createdBy,
            input: new LogFormattingInput(),
            settings: new LogFormattingSettings(),
            plan: CreatePlan());
    }

    /// <summary>
    /// Builds the single-job plan that writes the log formatting samples.
    /// </summary>
    /// <returns>A new immutable demonstration plan.</returns>
    public PipelinePlan CreatePlan()
    {
        var pipeline = builders.Create(Definition);

        pipeline.AddJob("formatting")
            .Step<LogFormattingStep>("write-formatting-samples")
            .Execute((step, ct) => step.ExecuteAsync(ct));

        return pipeline.Build();
    }
}