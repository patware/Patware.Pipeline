using System.Text.Json;

namespace Pipeline.Core;

/// <summary>
/// Packages a plan, serialized input and settings, and user-facing metadata for submission.
/// </summary>
/// <param name="Definition">The pipeline definition whose identity and version must match the executable plan.</param>
/// <param name="Title">The user-facing title of this particular run.</param>
/// <param name="CreatedBy">The caller-supplied identity of the person or service that submitted the run.</param>
/// <param name="InputJson">The serialized pipeline input used when restoring the plan.</param>
/// <param name="SettingsJson">The serialized pipeline settings used when restoring the plan.</param>
/// <param name="Plan">The executable graph to initialize for the submitted run.</param>
public sealed record PipelineRequest(
    PipelineDefinition Definition,
    string Title,
    string CreatedBy,
    string InputJson,
    string SettingsJson,
    PipelinePlan Plan)
{
    /// <summary>
    /// Creates a request by serializing input and settings and verifying that its definition matches the plan.
    /// </summary>
    /// <typeparam name="TInput">The input value type.</typeparam>
    /// <typeparam name="TSettings">The pipeline settings type.</typeparam>
    /// <param name="definition">The versioned pipeline definition associated with this plan or run.</param>
    /// <param name="title">The user-facing title of this particular run.</param>
    /// <param name="createdBy">The caller-supplied identity of the person or service that submitted the run.</param>
    /// <param name="input">The pipeline input to serialize using the default System.Text.Json options.</param>
    /// <param name="settings">The pipeline settings to serialize for plan restoration.</param>
    /// <param name="plan">The executable graph to initialize for the submitted run.</param>
    /// <returns>The request ready to submit to a pipeline runtime.</returns>
    /// <exception cref="ArgumentNullException">The definition or plan is null.</exception>
    /// <exception cref="ArgumentException">The title or submitter identity is blank, or the definition does not match the plan.</exception>
    /// <remarks>Input and settings must be serializable with the default <see cref="JsonSerializer" /> options and restorable by the matching definition registration.</remarks>
    public static PipelineRequest Create<TInput, TSettings>(
        PipelineDefinition definition,
        string title,
        string createdBy,
        TInput input,
        TSettings settings,
        PipelinePlan plan)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (definition != plan.Definition)
        {
            throw new ArgumentException(
                "The request and plan definitions do not match.");
        }

        return new PipelineRequest(
            definition,
            title,
            createdBy,
            JsonSerializer.Serialize(input),
            JsonSerializer.Serialize(settings),
            plan);
    }
}