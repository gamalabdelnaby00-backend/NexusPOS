using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Employees;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _db;

    public EmployeeRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> CodeExistsAsync(string code) =>
        _db.Employees.AsNoTracking().AnyAsync(e => e.Code == code);

    public Task<bool> UserNameExistsAsync(string userName) =>
        _db.Users.AsNoTracking().AnyAsync(u => u.UserName == userName);

    public async Task<IReadOnlyList<BranchOption>> GetActiveBranchesAsync() =>
        await _db.Branches.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Name)
            .Select(b => new BranchOption(b.Id, b.Code, b.Name)).ToListAsync();

    public Task<bool> BranchIsActiveAsync(int branchId) =>
        _db.Branches.AsNoTracking().AnyAsync(b => b.Id == branchId && b.IsActive);

    public async Task<IReadOnlyList<string>> GetActiveRoleNamesAsync() =>
        await _db.Roles.AsNoTracking().Where(r => r.IsActive).Select(r => r.Name).ToListAsync();

    public async Task<int?> FindRoleIdAsync(string roleName)
    {
        var id = await _db.Roles.AsNoTracking().Where(r => r.IsActive && r.Name == roleName)
            .Select(r => (int?)r.Id).FirstOrDefaultAsync();
        return id;
    }

    public async Task AddAsync(Employee employee) => await _db.Employees.AddAsync(employee);

    public async Task AddUserAsync(User user) => await _db.Users.AddAsync(user);

    public async Task AddUserRoleAsync(UserRole userRole) => await _db.UserRoles.AddAsync(userRole);

    public async Task SaveChangesAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            string message = sql.Message;
            string field = message.Contains("Users", StringComparison.OrdinalIgnoreCase) &&
                           message.Contains("UserName", StringComparison.OrdinalIgnoreCase) ? "UserName" : "Code";
            throw new DuplicateValueException(field);
        }
    }

    public async Task<IReadOnlyList<EmployeeAccountRow>> SearchAsync(string? text)
    {
        var employees = await _db.Employees.AsNoTracking().ToListAsync();
        var branches = await _db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name);
        var users = await _db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync();

        static string RolesOf(User u) => string.Join(", ",
            u.UserRoles.Where(ur => ur.Role is not null && ur.Role.IsActive).Select(ur => ur.Role!.Name).OrderBy(n => n));

        var rows = new List<EmployeeAccountRow>();

        foreach (var e in employees)
        {
            var user = users.FirstOrDefault(u => u.EmployeeId == e.Id);
            rows.Add(new EmployeeAccountRow(e.Code, e.ArabicName, e.EnglishName, user?.UserName, e.JobTitle,
                branches.GetValueOrDefault(e.BranchId), e.HireDate, user?.IsActive, user is null ? string.Empty : RolesOf(user)));
        }

        // Accounts that have no employee record (e.g. the initial Admin).
        foreach (var u in users.Where(u => u.EmployeeId is null))
            rows.Add(new EmployeeAccountRow("-", u.DisplayName, u.DisplayName, u.UserName, null, null, null, u.IsActive, RolesOf(u)));

        if (!string.IsNullOrWhiteSpace(text))
        {
            string q = text.Trim();
            bool Hit(string? v) => v is not null && v.Contains(q, StringComparison.OrdinalIgnoreCase);
            rows = rows.Where(r => Hit(r.EmployeeCode) || Hit(r.ArabicName) || Hit(r.EnglishName) || Hit(r.UserName) ||
                                   Hit(r.BranchName) || Hit(r.JobTitle) ||
                                   (r.JobTitle is not null && (Hit(JobTitles.Display(r.JobTitle, true)) || Hit(JobTitles.Display(r.JobTitle, false)))))
                .ToList();
        }

        return rows.OrderBy(r => r.EmployeeCode == "-").ThenBy(r => r.EmployeeCode).ToList();
    }
}
