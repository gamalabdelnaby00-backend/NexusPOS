using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;
    public UserRepository(AppDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(int userId) => _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

    public Task<User?> GetByUserNameAsync(string userName) => _context.Users
        // Split the collection includes to avoid cartesian expansion for users with many permissions.
        .AsSplitQuery()
        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r!.RolePermissions).ThenInclude(rp => rp.Permission)
        .Include(u => u.PermissionOverrides).ThenInclude(o => o.Permission)
        .FirstOrDefaultAsync(u => u.UserName == userName);

    public async Task UpdateAsync(User user) { _context.Users.Update(user); await _context.SaveChangesAsync(); }

    public async Task<IReadOnlyList<UserAccountRow>> SearchAccountsAsync(string? query)
    {
        var users = await _context.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.UserName).ToListAsync();
        var employees = await _context.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => new { e.Code, e.FullName });
        var rows = users.Select(u =>
        {
            var emp = u.EmployeeId is int id && employees.TryGetValue(id, out var found) ? found : null;
            var roles = string.Join(", ", u.UserRoles.Where(x => x.Role is { IsActive: true }).Select(x => x.Role!.Name).OrderBy(x => x));
            return new UserAccountRow(u.Id, u.UserName, u.DisplayName,
                emp is null ? "-" : $"{emp.Code} - {emp.FullName}", roles, u.IsActive, u.CreatedAtUtc, u.LastLoginAtUtc);
        }).ToList();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            rows = rows.Where(x => x.UserName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) || x.EmployeeName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Roles.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return rows;
    }

    public async Task<IReadOnlyList<AccountEmployeeOption>> GetEmployeesWithoutAccountsAsync() =>
        await _context.Employees.AsNoTracking().Where(e => e.IsActive && !_context.Users.Any(u => u.EmployeeId == e.Id))
            .OrderBy(e => e.Code).Select(e => new AccountEmployeeOption(e.Id, e.Code, e.FullName)).ToListAsync();

    public async Task<IReadOnlyList<AccountRoleOption>> GetActiveRolesAsync() =>
        await _context.Roles.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Name)
            .Select(r => new AccountRoleOption(r.Id, r.Name)).ToListAsync();

    public Task<bool> UserNameExistsAsync(string userName) => _context.Users.AnyAsync(u => u.UserName == userName);
    public Task<bool> EmployeeHasAccountAsync(int employeeId) => _context.Users.AnyAsync(u => u.EmployeeId == employeeId);
    public Task<string?> GetEmployeeDisplayNameAsync(int employeeId) => _context.Employees.AsNoTracking()
        .Where(e => e.Id == employeeId && e.IsActive).Select(e => (string?)e.FullName).FirstOrDefaultAsync();
    public Task<int?> GetActiveRoleIdAsync(string roleName) => _context.Roles.Where(r => r.IsActive && r.Name == roleName).Select(r => (int?)r.Id).FirstOrDefaultAsync();
    public async Task AddAccountAsync(User user) => await _context.Users.AddAsync(user);
    public async Task AddUserRoleAsync(UserRole userRole) => await _context.UserRoles.AddAsync(userRole);

    public async Task<IReadOnlyList<AccountRoleOption>> GetAssignedRolesAsync(int userId) =>
        await _context.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => new AccountRoleOption(ur.RoleId, ur.Role!.Name))
            .ToListAsync();

    public async Task RemoveUserRolesAsync(int userId)
    {
        var assignments = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        _context.UserRoles.RemoveRange(assignments);
    }
    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
