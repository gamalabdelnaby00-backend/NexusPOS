using UniversalPOS.Application.Security;

namespace UniversalPOS.Application.Authentication;

public enum StartScreen
{
    /// <summary>Account has no active role, so it cannot use the application.</summary>
    None,
    /// <summary>Open or resume a cash shift first (cashier flow).</summary>
    OpenShift,
    /// <summary>Main dashboard; visible sections depend on the user's permissions.</summary>
    Main
}

/// <summary>Chooses the first screen after sign-in from roles and permissions, not from hard-coded role names.</summary>
public static class StartScreenResolver
{
    public static StartScreen Resolve(CurrentUserSession session)
    {
        if (!session.IsAuthenticated || session.Roles.Count == 0)
            return StartScreen.None;

        if (session.Roles.Contains(RoleHierarchy.Admin, StringComparer.OrdinalIgnoreCase))
            return StartScreen.Main;

        bool canSell = session.Permissions.Contains("Sales.Create", StringComparer.OrdinalIgnoreCase);
        bool canOpenShift = session.Permissions.Contains("Shifts.Open", StringComparer.OrdinalIgnoreCase);

        return canSell && canOpenShift ? StartScreen.OpenShift : StartScreen.Main;
    }
}
