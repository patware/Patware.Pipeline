namespace Pipeline.Web.Services;

public interface IMsGraph
{
    Task<DTO.MsGraphUser?> GetUserAsync(string upn);
}
