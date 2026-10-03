namespace Pipeline.Web.DTO;

public class MsGraphUser
{
    public required string Upn { get; init; }
    public bool Enabled { get; set; }
    public IList<string> Licenses { get; set; } = [];
}
