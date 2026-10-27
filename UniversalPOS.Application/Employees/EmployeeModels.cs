namespace UniversalPOS.Application.Employees;

public sealed record AccountRequest(string UserName, string Password, string RoleName);

public sealed record CreateEmployeeRequest(
    string Code, string ArabicName, string EnglishName, DateOnly? BirthDate, string Phone,
    string JobTitle, int? BranchId, string Qualification, string Address, DateOnly? HireDate,
    AccountRequest? Account);

/// <summary>Errors map a field name (Code, ArabicName, ...) to a message key that the UI localizes.</summary>
public sealed class CreateEmployeeResult
{
    public bool Success { get; init; }
    public int EmployeeId { get; init; }
    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();
}

public sealed record BranchOption(int Id, string Code, string Name);

public sealed record EmployeeAccountRow(
    string EmployeeCode, string ArabicName, string EnglishName, string? UserName,
    string? JobTitle, string? BranchName, DateOnly? HireDate, bool? AccountActive, string Roles);
