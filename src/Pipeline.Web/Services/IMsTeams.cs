namespace Pipeline.Web.Services;

public interface IMsTeams
{
    Task AssignCallingPolicyAsync(string upn, string v);
    Task AssignLineAsync(string upn, ulong phoneNumber);
    Task<DTO.MsTeamsUser?> GetUserAsync(string upn);
}
