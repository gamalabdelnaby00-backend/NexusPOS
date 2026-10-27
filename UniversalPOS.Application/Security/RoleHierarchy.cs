namespace UniversalPOS.Application.Security;

/// <summary>
/// Administrative ranking: Admin &gt; Manager &gt; every other role. Used to stop a manager from
/// touching admin accounts, and to stop anyone from granting more than they hold.
/// </summary>
public static class RoleHierarchy
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";

    public static int RankOf(string roleName)
    {
        if (string.Equals(roleName, Admin, StringComparison.OrdinalIgnoreCase)) return 100;
        if (string.Equals(roleName, Manager, StringComparison.OrdinalIgnoreCase)) return 50;
        return 10;
    }

    public static int HighestRank(IEnumerable<string> roleNames) =>
        roleNames.Select(RankOf).DefaultIfEmpty(0).Max();

    /// <summary>
    /// True when the actor may modify (edit, disable, change roles of) an account holding the target roles.
    /// Admin may manage anyone; everyone else may only manage strictly lower-ranked accounts.
    /// </summary>
    public static bool CanManageAccount(IEnumerable<string> actorRoles, IEnumerable<string> targetRoles)
    {
        int actor = HighestRank(actorRoles);
        if (actor >= RankOf(Admin)) return true;
        return actor > HighestRank(targetRoles);
    }

    /// <summary>True when the actor may grant the named role.</summary>
    public static bool CanAssignRole(IEnumerable<string> actorRoles, string roleName)
    {
        int actor = HighestRank(actorRoles);
        if (actor >= RankOf(Admin)) return true;
        return actor > RankOf(roleName);
    }
}
