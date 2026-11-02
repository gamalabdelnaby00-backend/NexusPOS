using System.Text.RegularExpressions;
using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Authentication;

public sealed class UserAccountManagementService
{
    private static readonly Regex UserNamePattern = new("^[A-Za-z0-9._\\-]{3,50}$", RegexOptions.Compiled);
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogRepository _audit;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;

    public UserAccountManagementService(IUserRepository users, IUnitOfWork unitOfWork, IAuditLogRepository audit,
        CurrentUserSession session, PermissionChecker permissions)
    { _users = users; _unitOfWork = unitOfWork; _audit = audit; _session = session; _permissions = permissions; }

    public async Task<IReadOnlyList<UserAccountRow>> SearchAsync(string? query)
    {
        _permissions.DemandPermission(PermissionCodes.Users.View);
        return await _users.SearchAccountsAsync(query);
    }

    public async Task<IReadOnlyList<AccountEmployeeOption>> GetAvailableEmployeesAsync()
    {
        _permissions.DemandPermission(PermissionCodes.Users.Create);
        return await _users.GetEmployeesWithoutAccountsAsync();
    }

    public async Task<IReadOnlyList<AccountRoleOption>> GetAssignableRolesAsync()
    {
        if (!_permissions.HasPermission(PermissionCodes.Users.Create) && !_permissions.HasPermission(PermissionCodes.Users.Edit))
            throw new UnauthorizedAccessException("لا تملك صلاحية إنشاء الحسابات أو تعديلها.");
        var all = await _users.GetActiveRolesAsync();
        return all.Where(r => RoleHierarchy.CanAssignRole(_session.Roles, r.Name)).ToList();
    }

    public async Task CreateAsync(string userName, string password, int employeeId, string roleName)
    {
        _permissions.DemandPermission(PermissionCodes.Users.Create);
        if (!_session.IsAuthenticated || _session.UserId is not int actorId) throw new UnauthorizedAccessException();
        userName = (userName ?? string.Empty).Trim();
        if (!UserNamePattern.IsMatch(userName)) throw new ArgumentException("اسم المستخدم يجب أن يكون من 3 إلى 50 حرفًا إنجليزيًا أو رقمًا.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) throw new ArgumentException("كلمة المرور يجب ألا تقل عن 8 أحرف.");
        if (employeeId <= 0 || await _users.EmployeeHasAccountAsync(employeeId)) throw new InvalidOperationException("الموظف غير صالح أو لديه حساب بالفعل.");
        var displayName = await _users.GetEmployeeDisplayNameAsync(employeeId) ?? throw new InvalidOperationException("الموظف غير موجود أو غير نشط.");
        if (await _users.UserNameExistsAsync(userName)) throw new InvalidOperationException("اسم المستخدم مستخدم بالفعل.");
        if (!RoleHierarchy.CanAssignRole(_session.Roles, roleName)) throw new UnauthorizedAccessException("لا يمكنك منح هذا الدور.");
        var roleId = await _users.GetActiveRoleIdAsync(roleName) ?? throw new InvalidOperationException("الدور غير موجود أو غير نشط.");
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var user = new User(userName, displayName, PasswordHashing.Hash(password));
            user.LinkEmployee(employeeId);
            await _users.AddAccountAsync(user);
            await _users.SaveChangesAsync();
            await _users.AddUserRoleAsync(new UserRole(user.Id, roleId, actorId));
            await _audit.AddAsync(new AuditLog(actorId, "UserAccount.Create", nameof(User), user.Id.ToString(), $"username={userName}; employeeId={employeeId}; role={roleName}", null));
        });
    }

    public async Task ChangeRoleAsync(int userId, string newRoleName)
    {
        _permissions.DemandPermission(PermissionCodes.Users.Edit);
        if (!_session.IsAuthenticated || _session.UserId is not int actorId)
            throw new UnauthorizedAccessException();
        if (userId == actorId)
            throw new InvalidOperationException("لا يمكنك تغيير دور حسابك الحالي.");
        if (string.IsNullOrWhiteSpace(newRoleName))
            throw new ArgumentException("يجب اختيار دور صالح.", nameof(newRoleName));

        var target = await _users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("الحساب غير موجود.");
        var currentRoles = await _users.GetAssignedRolesAsync(userId);
        var currentRoleNames = currentRoles.Select(r => r.Name).ToArray();
        if (currentRoleNames.Length == 0)
            throw new InvalidOperationException("الحساب لا يحتوي على دور حالي.");
        if (!RoleHierarchy.CanManageAccount(_session.Roles, currentRoleNames))
            throw new UnauthorizedAccessException("لا يمكنك تغيير دور حساب أعلى منك أو مساويًا لك.");
        if (!RoleHierarchy.CanAssignRole(_session.Roles, newRoleName))
            throw new UnauthorizedAccessException("لا يمكنك منح هذا الدور.");

        var roleId = await _users.GetActiveRoleIdAsync(newRoleName)
            ?? throw new InvalidOperationException("الدور غير موجود أو غير نشط.");
        if (currentRoleNames.Any(r => string.Equals(r, newRoleName, StringComparison.OrdinalIgnoreCase)))
            return;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _users.RemoveUserRolesAsync(userId);
            await _users.AddUserRoleAsync(new UserRole(target.Id, roleId, actorId));
            await _users.SaveChangesAsync();
            await _audit.AddAsync(new AuditLog(actorId, "UserAccount.ChangeRole", nameof(User), userId.ToString(),
                $"roles={string.Join(",", currentRoleNames)}; newRole={newRoleName.Trim()}", null));
        });
    }

    public async Task SetActiveAsync(int userId, bool active)
    {
        _permissions.DemandPermission(active ? PermissionCodes.Users.Edit : PermissionCodes.Users.Deactivate);
        if (!_session.IsAuthenticated || _session.UserId is not int actorId) throw new UnauthorizedAccessException();
        if (userId == actorId && !active) throw new InvalidOperationException("لا يمكنك تعطيل حسابك الحالي.");
        var target = await _users.GetByIdAsync(userId) ?? throw new InvalidOperationException("الحساب غير موجود.");
        var roles = (await _users.SearchAccountsAsync(null)).FirstOrDefault(x => x.Id == userId)?.Roles
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (!RoleHierarchy.CanManageAccount(_session.Roles, roles)) throw new UnauthorizedAccessException("لا يمكنك تعديل حساب أعلى منك أو مساويًا لك.");
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (active) target.Activate(); else target.Deactivate();
            await _users.UpdateAsync(target);
            await _audit.AddAsync(new AuditLog(actorId, active ? "UserAccount.Activate" : "UserAccount.Deactivate", nameof(User), userId.ToString(), $"username={target.UserName}", null));
        });
    }

    public async Task ResetPasswordAsync(int userId, string newPassword)
    {
        _permissions.DemandPermission(PermissionCodes.Users.ResetPassword);
        if (!_session.IsAuthenticated || _session.UserId is not int actorId) throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8) throw new ArgumentException("كلمة المرور يجب ألا تقل عن 8 أحرف.");
        var target = await _users.GetByIdAsync(userId) ?? throw new InvalidOperationException("الحساب غير موجود.");
        var roles = (await _users.SearchAccountsAsync(null)).FirstOrDefault(x => x.Id == userId)?.Roles
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (!RoleHierarchy.CanManageAccount(_session.Roles, roles)) throw new UnauthorizedAccessException("لا يمكنك تعديل حساب أعلى منك أو مساويًا لك.");
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            target.ChangePasswordHash(PasswordHashing.Hash(newPassword));
            await _users.UpdateAsync(target);
            await _audit.AddAsync(new AuditLog(actorId, "UserAccount.ResetPassword", nameof(User), userId.ToString(), $"username={target.UserName}", null));
        });
    }
}
