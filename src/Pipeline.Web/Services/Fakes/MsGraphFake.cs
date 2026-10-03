using Pipeline.Web.DTO;

namespace Pipeline.Web.Services.Fakes;

public class MsGraphFake : Services.IMsGraph
{
    private readonly IList<DTO.MsGraphUser> _users = [
        new MsGraphUser{
            Upn = "Scott.Smith@home.net",
            Enabled = true,
            Licenses = ["E5", LicenseIds.PhoneSystem]
        },
        new MsGraphUser{
            Upn = "Patrice.Calve@home.net",
            Enabled = true,
            Licenses = ["E3", LicenseIds.PhoneSystem]
        },
        new MsGraphUser{
            Upn = "John.Doe@home.net",
            Enabled = true,
            Licenses = ["E3"]
        }
    ];

    public async Task<MsGraphUser?> GetUserAsync(string upn)
    {
        return _users.FirstOrDefault(u => string.Equals(upn, u.Upn, StringComparison.OrdinalIgnoreCase));
    }
}
