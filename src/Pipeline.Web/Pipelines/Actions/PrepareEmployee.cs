using Pipeline.Core;
using Pipeline.Web.Services;

namespace Pipeline.Web.Pipelines.Actions;

public sealed record PreparationResult(bool RequiresDirectorySync);

public class PrepareEmployee(Services.IActiveDirectory activeDirectory, Services.IMsGraph msGraph, Repository.IRepo repo, IPipelineStepLogger logger)
{
    // Move into a licensing configuration service when needed.
    private const string DefaultPhoneSystemGroup = "AD-P-LIC-M365_PhoneSystem_OrgAdm";

    public async Task<PreparationResult> ExecuteAsync(string upn, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Fetching {Style.Blue}'{upn}'{Style.Reset} details in MS Graph", cancellationToken);

        var graphUser = await msGraph.GetUserAsync(upn)
            ?? throw new InvalidOperationException($"User {Style.Blue}'{upn}'{Style.Reset} does not exist in MS Graph");

        if (!graphUser.Enabled)
        {
            throw new InvalidOperationException($"User {Style.Blue}'{upn}'{Style.Reset} is disabled");
        }

        var hasLicense = graphUser.Licenses.Contains(LicenseIds.PhoneSystem, StringComparer.OrdinalIgnoreCase);

        if (hasLicense)
        {
            await logger.InformationAsync($"User {Style.Blue}'{upn}'{Style.Reset} has a {LicenseIds.PhoneSystem}, all good", cancellationToken);
            return new PreparationResult(RequiresDirectorySync: false);
        }

        await logger.InformationAsync($"User {Style.Blue}'{upn}'{Style.Reset} doesn't have a {LicenseIds.PhoneSystem} according to MS Graph", cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        await logger.InformationAsync($"Fetching {Style.Blue}'{upn}'{Style.Reset} from Active Directory", cancellationToken);

        var adUser = await activeDirectory.GetUserAsync(upn)
            ?? throw new InvalidOperationException(
                $"User {Style.Blue}'{upn}'{Style.Reset} does not exist in Active Directory.");

        await logger.InformationAsync($"Fetching E3 to Phone System mapping", cancellationToken);
        var mappings = await repo.GetActiveDirectoryE3ToPhoneSystemMappingAsync();

        await logger.InformationAsync($"Checking if {Style.Blue}'{upn}'{Style.Reset} is a member of an AD group that has a eligible E3/E5 license", cancellationToken);
        var eligbleM365LicenseGroups = adUser.MemberOf
            .Where(group =>
                group.Contains("_E3_", StringComparison.OrdinalIgnoreCase) ||
                group.Contains("_E5_", StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();

        if (eligbleM365LicenseGroups.Length == 0)
        {
            throw new InvalidOperationException($"User {Style.Blue}'{upn}'{Style.Reset} has no eligible E3/E5 group.");
        }

        var matchingMappings = mappings
            .Where(map => eligbleM365LicenseGroups.Contains(map.E3, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var targetPhoneSystemAdGroups = matchingMappings
            .Select(map => map.PhoneSystem)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targetPhoneSystemAdGroups.Length > 1)
        {
            throw new InvalidOperationException($"User {Style.Blue}'{upn}'{Style.Reset} maps to multiple Phone System groups.");
        }

        var phoneSystemAdGroup = targetPhoneSystemAdGroups.SingleOrDefault()
            ?? DefaultPhoneSystemGroup;

        var alreadyMember = adUser.MemberOf.Contains(phoneSystemAdGroup, StringComparer.OrdinalIgnoreCase);

        if (!alreadyMember)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await logger.InformationAsync($"Adding {Style.Blue}'{upn}'{Style.Reset} to the AD Group {Style.Blue}'{phoneSystemAdGroup}'{Style.Reset} -> this will require a wait for AD to Entra Sync to occur.", cancellationToken);

            await activeDirectory.AddGroupMemberAsync(phoneSystemAdGroup, upn);

        }

        return new PreparationResult(RequiresDirectorySync: true);

    }
}
