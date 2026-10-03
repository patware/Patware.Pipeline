using Pipeline.Core;

namespace Pipeline.Web.Pipelines.Actions;

public class CheckEnterpriseVoiceEnabled(Services.IMsTeams msTeams, IPipelineStepLogger logger)
{

    public async Task<bool> ExecuteAsync(string upn, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Fetching {Style.Blue}'{upn}'{Style.Reset} from MS Teams", cancellationToken);

        var msTeamsUser = await msTeams.GetUserAsync(upn)
            ?? throw new Exception($"User {Style.Blue}'{upn}{Style.Reset}' not found in MS Teams");

        await logger.InformationAsync($"{Style.Blue}'{upn}'{Style.Reset} EnterpriseVoiceEnabled is [{Style.Blue}{msTeamsUser.EnterpriseVoiceEnabled}]{Style.Reset}", cancellationToken);

        return msTeamsUser.EnterpriseVoiceEnabled;

    }
}
