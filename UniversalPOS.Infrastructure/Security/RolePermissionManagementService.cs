using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Security;

using UniversalPOS.Application.Security;

public sealed record ManageableRoleOption(int Id, string Name, string? Description);
public sealed record RolePermissionItem(int PermissionId, string Code, string Name, string Module, string? Description, bool IsGranted);
public sealed record RolePermissionGroup(string Module, IReadOnlyList<RolePermissionItem> Permissions);

/// <summary>Loads and updates permissions assigned to a role, enforcing the current operator's authority.</summary>
public sealed class RolePermissionManagementService
{
    private readonly AppDbContext _db;
    private readonly CurrentUserSession _session;

    public RolePermissionManagementService(AppDbContext db, CurrentUserSession session)
    {
        _db = db;
        _session = session;
    }

    public async Task<IReadOnlyList<ManageableRoleOption>> GetManageableRolesAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanManagePermissions();
        var roles = await _db.Roles.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new ManageableRoleOption(x.Id, x.Name, x.Description))
            .ToListAsync(cancellationToken);
        return roles.Where(x => RoleHierarchy.CanAssignRole(_session.Roles, x.Name)).ToArray();
    }

    public async Task<IReadOnlyList<RolePermissionGroup>> GetRolePermissionsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        EnsureCanManagePermissions();
        var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roleId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("الدور غير موجود أو غير نشط.");
        EnsureCanTargetRole(role.Name);

        var assigned = await _db.RolePermissions.AsNoTracking().Where(x => x.RoleId == roleId)
            .Select(x => x.PermissionId).ToListAsync(cancellationToken);
        var permissions = await _db.Permissions.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Module).ThenBy(x => x.Name)
            .Select(x => new RolePermissionItem(x.Id, x.Code, x.Name, x.Module, x.Description, assigned.Contains(x.Id)))
            .ToListAsync(cancellationToken);
        return permissions.GroupBy(x => x.Module)
            .Select(g => new RolePermissionGroup(g.Key, g.ToArray())).ToArray();
    }

    public async Task SetPermissionAsync(int roleId, int permissionId, bool granted, CancellationToken cancellationToken = default)
    {
        EnsureCanManagePermissions();
        if (roleId <= 0 || permissionId <= 0) throw new ArgumentOutOfRangeException("معرّف الدور والصلاحية يجب أن يكونا أكبر من صفر.");

        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == roleId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("الدور غير موجود أو غير نشط.");
        var permission = await _db.Permissions.FirstOrDefaultAsync(x => x.Id == permissionId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("الصلاحية غير موجودة أو غير نشطة.");
        EnsureCanTargetRole(role.Name);

        if (granted && !RoleHierarchy.CanGrantPermission(_session.Roles, _session.Permissions, role.Name, permission.Code))
            throw new UnauthorizedAccessException("لا يمكنك منح صلاحية لا تملكها أو منحها لدور مساوٍ أو أعلى من دورك.");
        if (!granted && !RoleHierarchy.IsAdmin(_session.Roles) && !_session.Permissions.Contains(permission.Code, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("لا يمكنك تعديل صلاحية لا تملكها.");

        var link = await _db.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken);
        if (granted && link is null)
            await _db.RolePermissions.AddAsync(new RolePermission(roleId, permissionId), cancellationToken);
        else if (!granted && link is not null)
            _db.RolePermissions.Remove(link);
        else
            return;

        await _db.AuditLogs.AddAsync(new AuditLog(_session.UserId, granted ? "RolePermissionGranted" : "RolePermissionRevoked",
            "RolePermission", $"{roleId}:{permissionId}", $"Role={role.Name}; Permission={permission.Code}; Granted={granted}", null), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureCanManagePermissions()
    {
        if (!_session.IsAuthenticated) throw new UnauthorizedAccessException("يجب تسجيل الدخول أولاً.");
        if (RoleHierarchy.HighestRank(_session.Roles) < RoleHierarchy.RankOf(RoleHierarchy.Manager) ||
            (!RoleHierarchy.IsAdmin(_session.Roles) && !_session.Permissions.Contains(UniversalPOS.Application.Security.PermissionCodes.Roles.ManagePermissions, StringComparer.OrdinalIgnoreCase)))
            throw new UnauthorizedAccessException("لا تملك صلاحية إدارة صلاحيات الأدوار.");
    }

    private void EnsureCanTargetRole(string roleName)
    {
        if (!RoleHierarchy.CanAssignRole(_session.Roles, roleName))
            throw new UnauthorizedAccessException("لا يمكنك تعديل صلاحيات هذا الدور.");
    }
}
