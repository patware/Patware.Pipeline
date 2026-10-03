using Pipeline.Web.DTO;

namespace Pipeline.Web.Services.Fakes;

public class MsTeamsFake : Services.IMsTeams
{
    private readonly IList<DTO.MsTeamsUser> _users = [
        new MsTeamsUser{
            Upn = "Scott.Smith@home.net",
            PhoneNumber = 6139431000,
            EnterpriseVoiceEnabled = true
        },
        new MsTeamsUser{
            Upn = "Patrice.Calve@home.net",
            PhoneNumber = 6139431001,
            EnterpriseVoiceEnabled = true
        },
        new MsTeamsUser{
            Upn = "John.Doe@home.net",
            EnterpriseVoiceEnabled = false
        }
        ];


    public async Task<MsTeamsUser?> GetUserAsync(string upn)
    {
        return _users.FirstOrDefault(u => string.Equals(upn, u.Upn, StringComparison.OrdinalIgnoreCase));
    }

    public async Task AssignLineAsync(string upn, ulong phoneNumber)
    {
        var u = await GetUserAsync(upn) ?? throw new Exception($"User {upn} not found in MS Teams");

        u.PhoneNumber = phoneNumber;

    }

    public async Task DeassignLineAsync(string upn)
    {
        var u = await GetUserAsync(upn) ?? throw new Exception($"User {upn} not found in MS Teams");

        u.PhoneNumber = null;

    }

    public async Task AssignCallingPolicyAsync(string upn, string callingPolicyName)
    {
        var u = await GetUserAsync(upn) ?? throw new Exception($"User {upn} not found in MS Teams");

        u.CallingPolicyName = callingPolicyName;
    }
    public async Task DeassignCallingPolicyAsync(string upn)
    {
        var u = await GetUserAsync(upn) ?? throw new Exception($"User {upn} not found in MS Teams");

        u.CallingPolicyName = null;
    }
}
