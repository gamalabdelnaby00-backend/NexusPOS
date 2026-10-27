
using UniversalPOS.Application.Authentication;

namespace UniversalPOS.Application.Security;

public sealed class PermissionChecker
{
    private readonly CurrentUserSession _session;

    public PermissionChecker(CurrentUserSession session)
    {
        _session = session;
    }

    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            return false;

        if (!_session.IsAuthenticated)
            return false;

        // Admin automatically receives current and future permissions.
        if (_session.Roles.Contains(
                "Admin",
                StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return _session.Permissions.Contains(
            permissionCode,
            StringComparer.OrdinalIgnoreCase);
    }

    public void DemandPermission(string permissionCode)
    {
        if (!HasPermission(permissionCode))
        {
            throw new UnauthorizedAccessException(
                $"Permission denied: {permissionCode}");
        }
    }
}
