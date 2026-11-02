using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Employees;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Tests;

public sealed class FakeEmployeeRepository : IEmployeeRepository
{
    private int _nextId = 1;
    public List<Employee> Employees { get; } = new();
    public List<User> Users { get; } = new();
    public List<UserRole> UserRoles { get; } = new();

    /// <summary>Simulates losing a race: the database unique index rejects the insert at save time.</summary>
    public string? DuplicateFieldOnSave { get; set; }

    public Task<bool> CodeExistsAsync(string code) => Task.FromResult(Employees.Any(e => e.Code == code));
    public Task<string> GenerateNextCodeAsync() => Task.FromResult($"EMP-{Employees.Count + 1:D4}");
    public Task<bool> UserNameExistsAsync(string userName) => Task.FromResult(Users.Any(u => u.UserName == userName));

    public Task<Employee?> GetByCodeAsync(string code) =>
        Task.FromResult(Employees.FirstOrDefault(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<BranchOption>> GetActiveBranchesAsync() =>
        Task.FromResult<IReadOnlyList<BranchOption>>(new[] { new BranchOption(1, "MAIN", "Main Branch") });

    public Task<bool> BranchIsActiveAsync(int branchId) => Task.FromResult(branchId == 1);

    public Task<IReadOnlyList<DepartmentOption>> GetActiveDepartmentsAsync() =>
        Task.FromResult<IReadOnlyList<DepartmentOption>>(new[] { new DepartmentOption(1, "Sales") });
    public Task<bool> DepartmentIsActiveAsync(int departmentId) => Task.FromResult(departmentId == 1);
    public Task<IReadOnlyList<EmployeeListRow>> ListAsync() =>
        Task.FromResult<IReadOnlyList<EmployeeListRow>>(Employees.Select(e => new EmployeeListRow(
            e.Id, e.Code, e.FullName, e.PhotoKey, e.JobTitle, e.DepartmentId, null, "Main Branch", e.HireDate, e.IsActive)).ToList());

    public Task<IReadOnlyList<string>> GetActiveRoleNamesAsync() =>
        Task.FromResult<IReadOnlyList<string>>(new[] { "Admin", "Manager", "Accountant", "Cashier" });

    public Task<int?> FindRoleIdAsync(string roleName) =>
        Task.FromResult<int?>(roleName switch { "Admin" => 1, "Manager" => 2, "Accountant" => 3, "Cashier" => 4, _ => null });

    public Task AddAsync(Employee employee) { Employees.Add(employee); return Task.CompletedTask; }
    public Task AddUserAsync(User user) { Users.Add(user); return Task.CompletedTask; }
    public Task AddUserRoleAsync(UserRole userRole) { UserRoles.Add(userRole); return Task.CompletedTask; }

    public Task SaveChangesAsync()
    {
        if (DuplicateFieldOnSave is not null)
        {
            // The row never reaches the database.
            Employees.Clear();
            Users.Clear();
            throw new DuplicateValueException(DuplicateFieldOnSave);
        }

        foreach (var e in Employees.Where(e => e.Id == 0))
            typeof(Employee).GetProperty(nameof(Employee.Id))!.SetValue(e, _nextId++);
        foreach (var u in Users.Where(u => u.Id == 0))
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(u, _nextId++);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EmployeeAccountRow>> SearchAsync(string? text) =>
        Task.FromResult<IReadOnlyList<EmployeeAccountRow>>(Array.Empty<EmployeeAccountRow>());
}
