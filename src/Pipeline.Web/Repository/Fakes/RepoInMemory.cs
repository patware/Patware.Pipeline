using Pipeline.Web.Entities;

namespace Pipeline.Web.Repository.Fakes;

public class RepoInMemory : IRepo
{
    private readonly IReadOnlyList<Entities.ActiveDirectoryE3ToPhoneSystem> _activeDirectoryE3ToPhoneSystem = [
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_Foo", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_Foo"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_OrgAdm", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_OrgAdm"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_ITSEC", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_OrgAdm"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_Bar", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_Bar"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_VIP", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_VIP"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_ParlAm", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_ParlAm"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_PCentre", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_PCentre"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_Snafu_Admin", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_OrgAdm"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_Ed", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_Ed"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_RO", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_RO"},
        new ActiveDirectoryE3ToPhoneSystem{ Id = 1, E3 = "AD-P-LIC-M365_E3_PIT", PhoneSystem = "AD-P-LIC-M365_PhoneSystem_PIT"},
        ];
    public async Task<IList<ActiveDirectoryE3ToPhoneSystem>> GetActiveDirectoryE3ToPhoneSystemMappingAsync()
    {
        return [.. _activeDirectoryE3ToPhoneSystem];
    }
}
