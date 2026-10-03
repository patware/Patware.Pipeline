using Pipeline.Core;
using Pipeline.Web.Services;

namespace Pipeline.Web.Pipelines.Actions;

public class CheckPhoneSystemLicense(IMsGraph msGraph, IPipelineStepLogger logger)
{

    public async Task<bool> ExecuteAsync(string upn, string licenseId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Fetching {Style.Blue}'{upn}'{Style.Reset} from MsGraph", cancellationToken);

        var user = await msGraph.GetUserAsync(upn)
            ?? throw new InvalidOperationException($"User {Style.Blue}'{upn}'{Style.Reset} does not exist in MS Graph.");

        await logger.InformationAsync($"Checking if {Style.Blue}'{upn}'{Style.Reset} has the {Style.Blue}[{licenseId}]{Style.Reset} license", cancellationToken);
        return user.Licenses.Contains(licenseId, StringComparer.OrdinalIgnoreCase);
    }
}
