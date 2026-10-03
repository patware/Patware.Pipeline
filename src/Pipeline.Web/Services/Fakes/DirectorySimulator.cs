using Pipeline.Core;
using Pipeline.Web.DTO;

namespace Pipeline.Web.Services.Fakes;

public sealed record DirectorySyncResult(string Upn, string Licenses, bool EnterpriseVoiceEnabled);

public sealed class DirectorySimulator :
    IActiveDirectory,
    IMsGraph,
    IMsTeams
{
    private readonly object gate = new();

    private readonly Dictionary<string, AdUser> ad = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, MsGraphUser> graph = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, MsTeamsUser> teams = new(StringComparer.OrdinalIgnoreCase);

    public DirectorySimulator()
    {
        Seed(
            "Scott.Smith@home.net",
            ["AD-P-LIC-M365_E5_OrgAdm"],
            ["E5", LicenseIds.PhoneSystem],
            6139431000UL,
            true);

        Seed(
            "Patrice.Calve@home.net",
            [
                "AD-P-LIC-M365_E3_OrgAdm",
                "AD-P-LIC-M365_PhoneSystem_OrgAdm"
            ],
            ["E3", LicenseIds.PhoneSystem],
            6139431001UL,
            true);

        Seed(
            "John.Doe@home.net",
            ["AD-P-LIC-M365_E3_OrgAdm"],
            ["E3"],
            null,
            false);
    }

    private void Seed(
        string upn,
        string[] groups,
        string[] licenses,
        ulong? phone,
        bool voiceEnabled)
    {
        ad.Add(upn, new AdUser
        {
            UserPrincipalName = upn,
            MemberOf = groups.ToList()
        });

        graph.Add(upn, new MsGraphUser
        {
            Upn = upn,
            Enabled = true,
            Licenses = licenses.ToList()
        });

        teams.Add(upn, new MsTeamsUser
        {
            Upn = upn,
            PhoneNumber = phone,
            EnterpriseVoiceEnabled = voiceEnabled
        });
    }

    // Existing pipeline interfaces

    Task<AdUser?> IActiveDirectory.GetUserAsync(string upn)
    {
        lock (gate)
        {
            var user = ad.GetValueOrDefault(Key(upn));

            return Task.FromResult(
                user is null ? null : Copy(user));
        }
    }

    Task<IList<AdUser>> IActiveDirectory.GetUsersAsync()
    {
        lock (gate)
        {
            IList<AdUser> users = ad.Values
                .Select(Copy)
                .ToList();

            return Task.FromResult(users);
        }
    }

    Task IActiveDirectory.AddGroupMemberAsync(string adGroup, string upn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adGroup);

        lock (gate)
        {
            if (!ad.TryGetValue(Key(upn), out var user))
                throw new InvalidOperationException(
                    $"User {Style.Blue}'{upn}'{Style.Reset} does not exist in simulated AD.");

            if (!user.MemberOf.Contains(
                adGroup,
                StringComparer.OrdinalIgnoreCase))
            {
                user.MemberOf.Add(adGroup);
            }
        }

        return Task.CompletedTask;
    }

    Task<MsGraphUser?> IMsGraph.GetUserAsync(string upn) => Task.FromResult(GetGraphUser(upn));

    Task<MsTeamsUser?> IMsTeams.GetUserAsync(string upn) => Task.FromResult(GetTeamsUser(upn));

    Task IMsTeams.AssignLineAsync(string upn, ulong phoneNumber)
    {
        lock (gate)
        {
            RequireTeamsUser(upn).PhoneNumber = phoneNumber;
        }

        return Task.CompletedTask;
    }

    Task IMsTeams.AssignCallingPolicyAsync(string upn, string callingPolicyName)
    {
        lock (gate)
        {
            RequireTeamsUser(upn).CallingPolicyName = callingPolicyName;
        }

        return Task.CompletedTask;
    }

    // Simulator CRUD: callers receive detached copies.

    public MsGraphUser? GetGraphUser(string upn)
    {
        lock (gate)
        {
            var user = graph.GetValueOrDefault(Key(upn));
            return user is null ? null : Copy(user);
        }
    }

    public MsTeamsUser? GetTeamsUser(string upn)
    {
        lock (gate)
        {
            var user = teams.GetValueOrDefault(Key(upn));
            return user is null ? null : Copy(user);
        }
    }

    public void SaveGraphUser(MsGraphUser user, bool create)
    {
        ArgumentNullException.ThrowIfNull(user);

        var key = Key(user.Upn);

        var replacement = new MsGraphUser
        {
            Upn = key,
            Enabled = user.Enabled,
            Licenses = user.Licenses
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        lock (gate)
        {
            CheckSave(graph.ContainsKey(key), create, key);
            graph[key] = replacement;
        }
    }

    public void SaveTeamsUser(MsTeamsUser user, bool create)
    {
        ArgumentNullException.ThrowIfNull(user);

        var key = Key(user.Upn);

        var replacement = new MsTeamsUser
        {
            Upn = key,
            PhoneNumber = user.PhoneNumber,
            EnterpriseVoiceEnabled = user.EnterpriseVoiceEnabled,
            CallingPolicyName =
                string.IsNullOrWhiteSpace(user.CallingPolicyName)
                    ? null
                    : user.CallingPolicyName.Trim()
        };

        lock (gate)
        {
            CheckSave(teams.ContainsKey(key), create, key);
            teams[key] = replacement;
        }
    }

    public bool DeleteGraphUser(string upn)
    {
        lock (gate)
        {
            return graph.Remove(Key(upn));
        }
    }

    public bool DeleteTeamsUser(string upn)
    {
        lock (gate)
        {
            return teams.Remove(Key(upn));
        }
    }

    // Manual synchronization

    public IReadOnlyList<DirectorySyncResult> SyncFromAd()
    {
        lock (gate)
        {
            var results = new List<DirectorySyncResult>();

            foreach (var source in ad.Values.OrderBy(user => user.UserPrincipalName, StringComparer.OrdinalIgnoreCase))
            {
                var upn = source.UserPrincipalName;

                var hasE3 = HasGroupPrefix(source, "AD-P-LIC-M365_E3_");

                var hasE5 = HasGroupPrefix(source, "AD-P-LIC-M365_E5_");

                // Matches this PoC's seeded E5 user and group convention.
                var hasPhoneSystem = hasE5 || HasGroupPrefix(source, "AD-P-LIC-M365_PhoneSystem_");

                if (!graph.TryGetValue(upn, out var graphUser))
                {
                    graphUser = new MsGraphUser
                    {
                        Upn = upn,
                        Enabled = true
                    };

                    graph.Add(upn, graphUser);
                }

                // Sync owns these three simulated license values.
                // Preserve any other licenses entered through the editor.
                var licenses = new HashSet<string>(
                    graphUser.Licenses,
                    StringComparer.OrdinalIgnoreCase);

                licenses.Remove("E3");
                licenses.Remove("E5");
                licenses.Remove(LicenseIds.PhoneSystem);

                if (hasE3) licenses.Add("E3");
                if (hasE5) licenses.Add("E5");
                if (hasPhoneSystem) licenses.Add(LicenseIds.PhoneSystem);

                graphUser.Licenses = licenses
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!teams.TryGetValue(upn, out var teamsUser))
                {
                    teamsUser = new MsTeamsUser { Upn = upn };
                    teams.Add(upn, teamsUser);
                }

                // Simulation rule for downstream Teams provisioning.
                teamsUser.EnterpriseVoiceEnabled =
                    graphUser.Enabled && hasPhoneSystem;

                results.Add(new DirectorySyncResult(
                    upn,
                    string.Join(", ", graphUser.Licenses),
                    teamsUser.EnterpriseVoiceEnabled));
            }

            return results;
        }
    }

    private MsTeamsUser RequireTeamsUser(string upn)
    {
        return teams.GetValueOrDefault(Key(upn))
            ?? throw new InvalidOperationException(
                $"User {Style.Blue}'{upn}'{Style.Reset} does not exist in simulated Teams.");
    }

    private static bool HasGroupPrefix(AdUser user, string prefix) => user.MemberOf.Any(group => group.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string Key(string upn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(upn);
        return upn.Trim();
    }

    private static void CheckSave(
        bool exists,
        bool create,
        string upn)
    {
        if (create && exists)
            throw new InvalidOperationException(
                $"User {Style.Blue}'{upn}'{Style.Reset} already exists. Load it before editing.");

        if (!create && !exists)
            throw new InvalidOperationException(
                $"User {Style.Blue}'{upn}'{Style.Reset} no longer exists. Load it again to create it.");
    }

    private static AdUser Copy(AdUser user) => new()
    {
        UserPrincipalName = user.UserPrincipalName,
        MemberOf = user.MemberOf.ToList()
    };

    private static MsGraphUser Copy(MsGraphUser user) => new()
    {
        Upn = user.Upn,
        Enabled = user.Enabled,
        Licenses = user.Licenses.ToList()
    };

    private static MsTeamsUser Copy(MsTeamsUser user) => new()
    {
        Upn = user.Upn,
        PhoneNumber = user.PhoneNumber,
        EnterpriseVoiceEnabled = user.EnterpriseVoiceEnabled,
        CallingPolicyName = user.CallingPolicyName
    };
}