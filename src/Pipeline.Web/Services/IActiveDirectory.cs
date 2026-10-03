namespace Pipeline.Web.Services;

public interface IActiveDirectory
{

    Task<IList<DTO.AdUser>> GetUsersAsync();
    Task<DTO.AdUser?> GetUserAsync(string upn);
    Task AddGroupMemberAsync(string adGroup, string upn);
}
