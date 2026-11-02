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

    /// <summary>Returns true when the account has the Admin role.</summary>
    public static bool IsAdmin(IEnumerable<string> roleNames) =>
        roleNames.Any(role => string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns true when the supplied role name is Admin.</summary>
    public static bool IsAdmin(string? roleName) =>
        string.Equals(roleName, Admin, StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// True when the actor may grant a permission to the target role. Non-admins may only
    /// grant permissions they themselves hold, and only to roles below their own rank.
    /// </summary>
    public static bool CanGrantPermission(
        IEnumerable<string> actorRoles,
        IEnumerable<string> actorPermissions,
        string targetRoleName,
        string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(targetRoleName) || string.IsNullOrWhiteSpace(permissionCode))
            return false;

        if (HighestRank(actorRoles) >= RankOf(Admin))
            return true;

        return CanAssignRole(actorRoles, targetRoleName) &&
            actorPermissions.Any(permission => string.Equals(
                permission, permissionCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when the actor may grant the requested role/permission set without exceeding their authority.</summary>
    public static bool CanGrantPermission(IEnumerable<string> actorRoles, string roleName) =>
        CanAssignRole(actorRoles, roleName);

    public static bool CanGrantPermission(IEnumerable<string> actorRoles, IEnumerable<string> roleNames) =>
        roleNames.All(roleName => CanAssignRole(actorRoles, roleName));
}
