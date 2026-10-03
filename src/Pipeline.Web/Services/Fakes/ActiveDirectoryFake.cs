using Pipeline.Web.DTO;

namespace Pipeline.Web.Services.Fakes;

public class ActiveDirectoryFake : Pipeline.Web.Services.IActiveDirectory
{
    private readonly IList<AdUser> _users = [
        new AdUser {
            UserPrincipalName = "Scott.Smith@home.net",
            MemberOf = ["AD-P-LIC-M365_E5_OrgAdm"]
        },
        new AdUser {
            UserPrincipalName = "Patrice.Calve@home.net",
            MemberOf = [
                "AD-P-LIC-M365_E3_OrgAdm",
                "AD-P-LIC-M365_PhoneSystem_OrgAdm"
            ]
        },
        new AdUser {
            UserPrincipalName = "John.Doe@home.net",
            MemberOf = [
                "AD-P-LIC-M365_E3_OrgAdm"
            ]
        }

    ];

    public async Task AddGroupMemberAsync(string adGroup, string upn)
    {
        var u = await GetUserAsync(upn) ?? throw new Exception($"User with upn {upn} not found in AD");

        u.MemberOf.Add(adGroup);

        return;
    }

    public async Task<AdUser?> GetUserAsync(string upn)
    {
        return _users.FirstOrDefault(u => string.Equals(upn, u.UserPrincipalName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IList<AdUser>> GetUsersAsync()
    {
        return [.. _users];
    }
}
