using Pipeline.Core;

namespace Pipeline.Web.Pipelines.Actions;

public class AssignPhoneNumber(Services.IMsTeams msTeams, IPipelineStepLogger logger)
{

    public async Task ExecuteAsync(string upn, ulong phoneNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Fetching {Style.Blue}'{upn}'{Style.Reset} from MS Teams", cancellationToken);

        var user = await msTeams.GetUserAsync(upn)
            ?? throw new InvalidOperationException($"User {Style.Blue}'{upn}'{Style.Reset} does not exist in MS Teams.");

        if (user.PhoneNumber == phoneNumber)
        {
            await logger.InformationAsync($"User {Style.Blue}'{upn}'{Style.Reset} already has Phone Number {Style.Blue}[{phoneNumber}]{Style.Reset}", cancellationToken);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Assigning phone number {Style.Blue}'{phoneNumber}'{Style.Reset} to {Style.Blue}'{upn}'{Style.Reset}", cancellationToken);

        await msTeams.AssignLineAsync(upn, phoneNumber);
    }
}
