using Pipeline.Runtime;

namespace Pipeline.Web.Services;

public interface IMyPipelines
{
    Task<PipelineRun> AssignLineEmployee(
        string upn, ulong phoneNumber);

    Task<PipelineRun> AssignLineMpAndStaff(
        string upn, ulong phoneNumber);

    Task<PipelineRun> DeassignLine(
        string upn, ulong phoneNumber);

    Task<PipelineRun> TransferLineEmployee(
        ulong phoneNumber, string fromUpn, string toUpn);

    Task<PipelineRun> TransferLineMpAndStaff(
        ulong phoneNumber, string fromUpn, string toUpn);

    Task<PipelineRun> DeployCallFlow(
        ulong phoneNumber,
        ulong publishedPhoneNumber,
        uint ebr,
        Guid locationId);
}
