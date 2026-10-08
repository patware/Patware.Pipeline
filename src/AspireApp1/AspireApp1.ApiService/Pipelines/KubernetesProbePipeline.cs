using Pipeline.Core;
using Pipeline.Runtime;

namespace AspireApp1.ApiService.Pipelines;

/// <summary>
/// Builds a delayed pipeline used to verify distributed execution.
/// </summary>
/// <param name="builders">
/// The factory creating independent pipeline plans.
/// </param>
/// <remarks>
/// The probe has no external business side effects.
/// Its delay provides time to inspect ownership and interrupt a worker.
/// Its definition and plan are identical on every backend replica.
/// </remarks>
internal sealed class KubernetesProbePipeline(IPipelineBuilderFactory builders)
{
    /// <summary>
    /// Gets the stable identity of the verification pipeline.
    /// </summary>
    public static PipelineDefinition Definition { get; } = new("KubernetesProbe", "Kubernetes distribution probe", 1);

    /// <summary>
    /// Creates a new verification request.
    /// </summary>
    /// <returns>The request submitted to the selected runtime.</returns>
    public PipelineRequest Build()
    {
        return PipelineRequest.Create(
            definition: Definition,
            title: "Kubernetes distribution probe",
            createdBy: $"Submission through {Environment.MachineName}",
            input: new { },
            settings: new { },
            plan: CreatePlan());
    }

    /// <summary>
    /// Builds the versioned verification plan.
    /// </summary>
    /// <returns>A new executable plan.</returns>
    public PipelinePlan CreatePlan()
    {
        var pipeline = builders.Create(Definition);

        pipeline.AddJob("probe")
            .Step<KubernetesProbeStep>("delayed-probe")
            .Execute((step, ct) => step.ExecuteAsync(ct));

        return pipeline.Build();
    }
}

/// <summary>
/// Restores the verification pipeline on any backend replica.
/// </summary>
/// <param name="pipeline">
/// The definition used to reconstruct the execution plan.
/// </param>
internal sealed class KubernetesProbeRegistration(KubernetesProbePipeline pipeline) : IPipelineDefinitionRegistration
{
    /// <inheritdoc />
    public string DefinitionId => KubernetesProbePipeline.Definition.Id;

    /// <inheritdoc />
    public int DefinitionVersion => KubernetesProbePipeline.Definition.Version;

    /// <inheritdoc />
    public PipelinePlan Restore(PipelineExecutionSpecification specification)
    {
        if (specification.DefinitionId != DefinitionId || specification.DefinitionVersion != DefinitionVersion)
        {
            throw new InvalidOperationException(
                "The specification does not match the probe definition.");
        }

        return pipeline.CreatePlan();
    }
}

/// <summary>
/// Records its executing pod and waits before completing.
/// </summary>
/// <param name="pipelineLogger">
/// The logger appending persisted invocation logs.
/// </param>
/// <param name="context">
/// The context identifying the current pipeline run.
/// </param>
/// <param name="logger">
/// The host logger used to identify the executing container.
/// </param>
/// <remarks>
/// Recovery may invoke this method again after an expired lease.
/// Repeated invocation is harmless because the probe only logs and waits.
/// </remarks>
internal sealed class KubernetesProbeStep(
    IPipelineStepLogger pipelineLogger,
    IPipelineStepContext context,
    ILogger<KubernetesProbeStep> logger)
{
    /// <summary>
    /// Records execution ownership and completes after one minute.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token cancelling the invocation.
    /// </param>
    /// <returns>A task representing probe execution.</returns>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var pod = Environment.MachineName;

        logger.LogInformation(
            "Probe run {RunId} started on {Pod}.",
            context.Current.RunId,
            pod);

        await pipelineLogger.InformationAsync(
            $"Probe started on {pod}.",
            cancellationToken);

        await Task.Delay(
            TimeSpan.FromSeconds(60),
            cancellationToken);

        await pipelineLogger.InformationAsync(
            $"Probe completed on {pod}.",
            cancellationToken);
    }
}