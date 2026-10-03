using Pipeline.Runtime;
using Pipeline.Web.Pipelines;

namespace Pipeline.Web.Services;

public sealed class MyPipelines(AssignLineEmployeeDefinition employeeDefinition,
    IPipelineRuntime runtime,
    IHttpContextAccessor httpContextAccessor) : IMyPipelines
{
    public Task<PipelineRun> AssignLineEmployee(string upn, ulong phoneNumber)
    {

        ArgumentException.ThrowIfNullOrWhiteSpace(upn);

        var name = httpContextAccessor
            .HttpContext?
            .User.Identity?
            .Name;

        var createdBy = string.IsNullOrWhiteSpace(name)
            ? "Unknown"
            : name;

        var request = employeeDefinition.Build(upn, phoneNumber, createdBy);

        return runtime.EnqueueAsync(request);
    }

    public Task<PipelineRun> AssignLineMpAndStaff(string upn, ulong phoneNumber)
    {
        throw new NotImplementedException();
    }

    public Task<PipelineRun> DeassignLine(string upn, ulong phoneNumber)
    {
        throw new NotImplementedException();
    }

    public Task<PipelineRun> DeployCallFlow(ulong phoneNumber, ulong publishedPhoneNumber, uint ebr, Guid locationId)
    {
        throw new NotImplementedException();
    }

    public Task<PipelineRun> TransferLineEmployee(ulong phoneNumber, string fromUpn, string toUpn)
    {
        throw new NotImplementedException();
    }

    public Task<PipelineRun> TransferLineMpAndStaff(ulong phoneNumber, string fromUpn, string toUpn)
    {
        throw new NotImplementedException();
    }
}
