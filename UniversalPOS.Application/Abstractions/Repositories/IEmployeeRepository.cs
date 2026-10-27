using UniversalPOS.Application.Employees;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface IEmployeeRepository
{
    Task<bool> CodeExistsAsync(string code);
    Task<bool> UserNameExistsAsync(string userName);
    Task<IReadOnlyList<BranchOption>> GetActiveBranchesAsync();
    Task<bool> BranchIsActiveAsync(int branchId);
    Task<IReadOnlyList<string>> GetActiveRoleNamesAsync();
    Task<int?> FindRoleIdAsync(string roleName);

    Task AddAsync(Employee employee);
    Task AddUserAsync(User user);
    Task AddUserRoleAsync(UserRole userRole);

    /// <summary>Saves; throws <see cref="Common.DuplicateValueException"/> if a unique index rejects the row.</summary>
    Task SaveChangesAsync();

    Task<IReadOnlyList<EmployeeAccountRow>> SearchAsync(string? text);
}
