namespace Pipeline.Web.DTO;

public class MsTeamsUser
{
    public required string Upn { get; set; }
    public ulong? PhoneNumber { get; set; }
    public bool EnterpriseVoiceEnabled { get; internal set; }
    public string? CallingPolicyName { get; set; }
}
