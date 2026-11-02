namespace UniversalPOS.Application.Employees;

public sealed record AccountRequest(string UserName, string Password, string RoleName);

/// <summary>Code may be left empty on create; the service then generates the next EMP-#### code.</summary>
public sealed record CreateEmployeeRequest(
    string Code, string FullName, string Phone, string Email,
    string JobTitle, int? BranchId, DateOnly? HireDate, string? PhotoKey,
    AccountRequest? Account, int? DepartmentId = null);

/// <summary>Errors map a field name (Code, FullName, ...) to a message key that the UI localizes.</summary>
public sealed class CreateEmployeeResult
{
    public bool Success { get; init; }
    public int EmployeeId { get; init; }
    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();
}

public sealed record BranchOption(int Id, string Code, string Name);

public sealed record EmployeeAccountRow(
    string EmployeeCode, string FullName, string? UserName,
    string? JobTitle, string? BranchName, DateOnly? HireDate, bool? AccountActive, string Roles,
    bool EmployeeActive = true, string Phone = "");

public sealed record DepartmentOption(int Id, string Name);

/// <summary>One row of the employees list screen.</summary>
public sealed record EmployeeListRow(
    int Id, string Code, string FullName, string? PhotoKey, string JobTitle,
    int? DepartmentId, string? DepartmentName, string BranchName, DateOnly HireDate, bool IsActive);
