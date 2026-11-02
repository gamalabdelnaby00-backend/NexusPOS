using UniversalPOS.Domain.Entities;
using UniversalPOS.Application.Authentication;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByUserNameAsync(string userName);
    Task UpdateAsync(User user);
    Task<IReadOnlyList<UserAccountRow>> SearchAccountsAsync(string? query);
    Task<IReadOnlyList<AccountEmployeeOption>> GetEmployeesWithoutAccountsAsync();
    Task<IReadOnlyList<AccountRoleOption>> GetActiveRolesAsync();
    Task<bool> UserNameExistsAsync(string userName);
    Task<bool> EmployeeHasAccountAsync(int employeeId);
    Task<string?> GetEmployeeDisplayNameAsync(int employeeId);
    Task<int?> GetActiveRoleIdAsync(string roleName);
    Task AddAccountAsync(User user);
    Task AddUserRoleAsync(UserRole userRole);
    Task<IReadOnlyList<AccountRoleOption>> GetAssignedRolesAsync(int userId);
    Task RemoveUserRolesAsync(int userId);
    Task SaveChangesAsync();
}
