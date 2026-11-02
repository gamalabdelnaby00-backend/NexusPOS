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

    public async Task<IReadOnlyList<DepartmentOption>> GetActiveDepartmentsAsync() =>
        await _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new DepartmentOption(d.Id, d.Name)).ToListAsync();

    public Task<bool> DepartmentIsActiveAsync(int departmentId) =>
        _db.Departments.AsNoTracking().AnyAsync(d => d.Id == departmentId && d.IsActive);

    public async Task<IReadOnlyList<EmployeeListRow>> ListAsync()
    {
        var rows = await (from e in _db.Employees.AsNoTracking()
                          join b in _db.Branches.AsNoTracking() on e.BranchId equals b.Id
                          join d in _db.Departments.AsNoTracking() on e.DepartmentId equals d.Id into departments
                          from d in departments.DefaultIfEmpty()
                          orderby e.Code
                          select new { e.Id, e.Code, e.FullName, e.PhotoKey, e.JobTitle, e.DepartmentId,
                                       DepartmentName = d == null ? null : d.Name, BranchName = b.Name, e.HireDate, e.IsActive }).ToListAsync();
        return rows.Select(r => new EmployeeListRow(r.Id, r.Code, r.FullName, r.PhotoKey, r.JobTitle,
            r.DepartmentId, r.DepartmentName, r.BranchName, r.HireDate, r.IsActive)).ToList();
    }

    public async Task<string> GenerateNextCodeAsync()
    {
        var codes = await _db.Employees.AsNoTracking().Where(e => e.Code.StartsWith("EMP-")).Select(e => e.Code).ToListAsync();
        int max = 0;
        foreach (var c in codes)
            if (int.TryParse(c.AsSpan(4), out int n) && n > max) max = n;
        return $"EMP-{max + 1:D4}";
    }

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

    public Task<Employee?> GetByCodeAsync(string code) => _db.Employees.FirstOrDefaultAsync(e => e.Code == code);

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
            rows.Add(new EmployeeAccountRow(e.Code, e.FullName, user?.UserName, e.JobTitle,
                branches.GetValueOrDefault(e.BranchId), e.HireDate, user?.IsActive, user is null ? string.Empty : RolesOf(user), e.IsActive, e.Phone));
        }

        // Accounts that have no employee record (e.g. the initial Admin).
        foreach (var u in users.Where(u => u.EmployeeId is null))
            rows.Add(new EmployeeAccountRow("-", u.DisplayName, u.UserName, null, null, null, u.IsActive, RolesOf(u), false, string.Empty));

        if (!string.IsNullOrWhiteSpace(text))
        {
            string q = text.Trim();
            bool Hit(string? v) => v is not null && v.Contains(q, StringComparison.OrdinalIgnoreCase);
            rows = rows.Where(r => Hit(r.EmployeeCode) || Hit(r.FullName) ||
                                   Hit(r.BranchName) || Hit(r.JobTitle) ||
                                   (r.JobTitle is not null && (Hit(JobTitles.Display(r.JobTitle, true)) || Hit(JobTitles.Display(r.JobTitle, false)))))
                .ToList();
        }

        return rows.OrderBy(r => r.EmployeeCode == "-").ThenBy(r => r.EmployeeCode).ToList();
    }
}
