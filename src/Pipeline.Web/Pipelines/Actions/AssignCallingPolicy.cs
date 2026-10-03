using Pipeline.Core;
using Pipeline.Web.Services;

namespace Pipeline.Web.Pipelines.Actions;

public sealed class AssignCallingPolicy(IMsTeams msTeams, IPipelineStepLogger logger)
{
    public async Task ExecuteAsync(
        string upn,
        string callingPolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Fetching {Style.Blue}'{upn}'{Style.Reset} from Active Directory", cancellationToken);

        var user = await msTeams.GetUserAsync(upn)
            ?? throw new InvalidOperationException($"User {Style.Blue}'{upn}'{Style.Reset} does not exist in MS Teams.");

        if (string.Equals(user.CallingPolicyName, callingPolicy, StringComparison.OrdinalIgnoreCase))
        {
            await logger.InformationAsync($"User {Style.Blue}'{upn}'{Style.Reset} already has the {Style.Blue}'{callingPolicy}'{Style.Reset} Calling Policy", cancellationToken);

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Assigning Calling Policy {Style.Blue}'{callingPolicy}'{Style.Reset} to {Style.Blue}'{upn}'{Style.Reset}", cancellationToken);

        await msTeams.AssignCallingPolicyAsync(upn, callingPolicy);
    }
}