using System.Text.Json;

using Pipeline.Core;
using Pipeline.Runtime;

namespace Pipeline.Web.Pipelines;

public sealed class AssignLineEmployeeRegistration(AssignLineEmployeeDefinition definition) : IPipelineDefinitionRegistration
{
    public string DefinitionId => AssignLineEmployeeDefinition.Definition.Id;

    public int DefinitionVersion => AssignLineEmployeeDefinition.Definition.Version;

    public PipelinePlan Restore(PipelineExecutionSpecification specification)
    {
        if (specification.DefinitionId != DefinitionId ||
            specification.DefinitionVersion != DefinitionVersion)
        {
            throw new InvalidOperationException("The specification does not match this registration.");
        }

        var input = JsonSerializer.Deserialize<AssignLineInput>(specification.InputJson)
            ?? throw new InvalidOperationException("Pipeline inputs are missing.");

        var settings =
            JsonSerializer.Deserialize<AssignLineEmployeeSettings>(specification.SettingsJson)
            ?? throw new InvalidOperationException("Pipeline settings are missing.");

        return definition.CreatePlan(input, settings);
    }
}
