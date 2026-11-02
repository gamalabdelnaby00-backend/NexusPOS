namespace UniversalPOS.Application.Authentication;

public sealed record UserAccountRow(int Id, string UserName, string DisplayName, string EmployeeName, string Roles, bool IsActive, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);
public sealed record AccountEmployeeOption(int Id, string Code, string Name);
public sealed record AccountRoleOption(int Id, string Name);
