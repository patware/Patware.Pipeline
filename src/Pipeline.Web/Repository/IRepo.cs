namespace Pipeline.Web.Repository;

public interface IRepo
{
    public Task<IList<Entities.ActiveDirectoryE3ToPhoneSystem>> GetActiveDirectoryE3ToPhoneSystemMappingAsync();

}
