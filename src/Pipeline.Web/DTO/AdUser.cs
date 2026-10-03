namespace Pipeline.Web.DTO;

public class AdUser
{
    public required string UserPrincipalName { get; set; }
    public IList<string> MemberOf { get; set; } = [];
}
