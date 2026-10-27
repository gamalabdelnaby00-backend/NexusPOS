using System.Text.RegularExpressions;
using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Employees;

/// <summary>Creates employees (and, optionally, their login account) with full validation and a unique business code.</summary>
public sealed class EmployeeService
{
    private static readonly Regex CodePattern = new(@"^[A-Za-z0-9_\-]{2,20}$", RegexOptions.Compiled);
    private static readonly Regex ArabicPattern = new(@"^[\u0600-\u06FF\s]+$", RegexOptions.Compiled);
    private static readonly Regex EnglishPattern = new(@"^[A-Za-z\s'\.\-]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?\d{8,15}$", RegexOptions.Compiled);
    private static readonly Regex UserNamePattern = new(@"^[A-Za-z0-9._\-]{3,50}$", RegexOptions.Compiled);

    private const int MinimumWorkingAge = 16;

    private readonly IEmployeeRepository _employees;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;
    private readonly TimeProvider _time;

    public EmployeeService(IEmployeeRepository employees, IAuditLogRepository audit, IUnitOfWork unitOfWork,
        CurrentUserSession session, PermissionChecker permissions, TimeProvider? time = null)
    {
        _employees = employees; _audit = audit; _unitOfWork = unitOfWork;
        _session = session; _permissions = permissions; _time = time ?? TimeProvider.System;
    }

    public Task<IReadOnlyList<BranchOption>> GetBranchesAsync() => _employees.GetActiveBranchesAsync();

    /// <summary>Roles the current user is allowed to hand out (Manager cannot grant Admin or Manager).</summary>
    public async Task<IReadOnlyList<string>> GetAssignableRolesAsync()
    {
        var all = await _employees.GetActiveRoleNamesAsync();
        return all.Where(r => RoleHierarchy.CanAssignRole(_session.Roles, r)).OrderBy(r => r).ToList();
    }

    public async Task<IReadOnlyList<EmployeeAccountRow>> SearchAsync(string? text)
    {
        if (!_permissions.HasPermission(PermissionCodes.Employees.View) &&
            !_permissions.HasPermission(PermissionCodes.Users.View))
            throw new UnauthorizedAccessException($"Permission denied: {PermissionCodes.Employees.View}");

        return await _employees.SearchAsync(text);
    }

    public async Task<CreateEmployeeResult> CreateAsync(CreateEmployeeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new InvalidOperationException("A signed-in user is required.");

        _permissions.DemandPermission(PermissionCodes.Employees.Create);
        if (request.Account is not null)
            _permissions.DemandPermission(PermissionCodes.Users.Create);

        int actorId = _session.UserId.Value;
        var errors = Validate(request);

        string code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();

        if (!errors.ContainsKey("Code") && await _employees.CodeExistsAsync(code))
            errors["Code"] = "DuplicateCode";

        if (request.BranchId is int branchId && !await _employees.BranchIsActiveAsync(branchId))
            errors["BranchId"] = "InvalidBranch";

        if (request.Account is not null)
        {
            if (!errors.ContainsKey("UserName") && await _employees.UserNameExistsAsync(request.Account.UserName.Trim()))
                errors["UserName"] = "DuplicateUserName";

            if (!RoleHierarchy.CanAssignRole(_session.Roles, request.Account.RoleName))
                errors["RoleName"] = "RoleNotAllowed";
        }

        if (errors.Count > 0)
            return Fail(errors);

        try
        {
            int employeeId = 0;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var employee = new Employee(code, request.ArabicName, request.EnglishName,
                    request.BirthDate!.Value, request.Phone.Replace(" ", "").Replace("-", ""),
                    request.JobTitle, request.BranchId!.Value, request.Qualification, request.Address,
                    request.HireDate!.Value);

                await _employees.AddAsync(employee);
                await _employees.SaveChangesAsync();   // obtain the internal Id
                employeeId = employee.Id;

                if (request.Account is not null)
                {
                    int roleId = await _employees.FindRoleIdAsync(request.Account.RoleName)
                        ?? throw new InvalidOperationException("Role was not found.");

                    var user = new User(request.Account.UserName.Trim(), employee.ArabicName,
                        PasswordHashing.Hash(request.Account.Password));
                    user.LinkEmployee(employee.Id);
                    await _employees.AddUserAsync(user);
                    await _employees.SaveChangesAsync();

                    await _employees.AddUserRoleAsync(new UserRole(user.Id, roleId, actorId));
                }

                await _audit.AddAsync(new AuditLog(actorId, "Employee.Create", nameof(Employee),
                    employeeId.ToString(),
                    $"code={code}; branch={request.BranchId}; account={(request.Account is null ? "none" : request.Account.UserName.Trim())}; role={request.Account?.RoleName ?? "-"}",
                    null));
            });

            return new CreateEmployeeResult { Success = true, EmployeeId = employeeId };
        }
        catch (DuplicateValueException ex)
        {
            // Two people saved the same code or username at the same moment; the database constraint decided.
            return Fail(new Dictionary<string, string>
            {
                [ex.Field == "UserName" ? "UserName" : "Code"] = ex.Field == "UserName" ? "DuplicateUserName" : "DuplicateCode"
            });
        }
    }

    private Dictionary<string, string> Validate(CreateEmployeeRequest r)
    {
        var errors = new Dictionary<string, string>();
        DateOnly today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

        string code = (r.Code ?? string.Empty).Trim();
        if (code.Length == 0) errors["Code"] = "Required";
        else if (!CodePattern.IsMatch(code)) errors["Code"] = "InvalidCode";

        ValidateName(errors, "ArabicName", r.ArabicName, ArabicPattern, "ArabicName3Words");
        ValidateName(errors, "EnglishName", r.EnglishName, EnglishPattern, "EnglishName3Words");

        if (r.BirthDate is null) errors["BirthDate"] = "Required";
        else if (r.BirthDate > today) errors["BirthDate"] = "FutureDate";
        else if (AgeOn(r.BirthDate.Value, today) < MinimumWorkingAge) errors["BirthDate"] = "TooYoung";

        string phone = (r.Phone ?? string.Empty).Replace(" ", "").Replace("-", "");
        if (phone.Length == 0) errors["Phone"] = "Required";
        else if (!PhonePattern.IsMatch(phone)) errors["Phone"] = "InvalidPhone";

        if (string.IsNullOrWhiteSpace(r.JobTitle)) errors["JobTitle"] = "Required";
        else if (!JobTitles.IsValid(r.JobTitle)) errors["JobTitle"] = "InvalidJob";

        if (r.BranchId is null) errors["BranchId"] = "Required";
        if (string.IsNullOrWhiteSpace(r.Qualification)) errors["Qualification"] = "Required";
        if (string.IsNullOrWhiteSpace(r.Address)) errors["Address"] = "Required";

        if (r.HireDate is null) errors["HireDate"] = "Required";
        else if (r.HireDate > today) errors["HireDate"] = "FutureDate";
        else if (r.BirthDate is not null && !errors.ContainsKey("BirthDate") &&
                 r.HireDate.Value < r.BirthDate.Value.AddYears(MinimumWorkingAge)) errors["HireDate"] = "HireBeforeWorkingAge";

        if (r.Account is not null)
        {
            if (!UserNamePattern.IsMatch((r.Account.UserName ?? string.Empty).Trim())) errors["UserName"] = "InvalidUserName";
            if ((r.Account.Password ?? string.Empty).Length < 8) errors["Password"] = "WeakPassword";
            if (string.IsNullOrWhiteSpace(r.Account.RoleName)) errors["RoleName"] = "Required";
        }

        return errors;
    }

    private static void ValidateName(Dictionary<string, string> errors, string field, string? value, Regex pattern, string wordsKey)
    {
        string name = (value ?? string.Empty).Trim();
        if (name.Length == 0) { errors[field] = "Required"; return; }
        if (!pattern.IsMatch(name)) { errors[field] = "InvalidCharacters"; return; }
        if (name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length < 3) errors[field] = wordsKey;
    }

    private static int AgeOn(DateOnly birth, DateOnly today)
    {
        int age = today.Year - birth.Year;
        if (today < birth.AddYears(age)) age--;
        return age;
    }

    private static CreateEmployeeResult Fail(IReadOnlyDictionary<string, string> errors) =>
        new() { Success = false, Errors = errors };
}
