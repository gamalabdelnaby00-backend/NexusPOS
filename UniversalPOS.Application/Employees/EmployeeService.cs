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
    // Letters (Arabic or Latin), spaces and common name punctuation only.
    private static readonly Regex NamePattern = new(@"^[\p{L}\s'\.\-]+$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?\d{8,15}$", RegexOptions.Compiled);
    private static readonly Regex UserNamePattern = new(@"^[A-Za-z0-9._\-]{3,50}$", RegexOptions.Compiled);

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

    public Task<IReadOnlyList<DepartmentOption>> GetDepartmentsAsync() => _employees.GetActiveDepartmentsAsync();

    /// <summary>Rows for the employees list screen (code, name, photo, department, job, hire date, status).</summary>
    public async Task<IReadOnlyList<EmployeeListRow>> ListAsync()
    {
        if (!_permissions.HasPermission(PermissionCodes.Employees.View) &&
            !_permissions.HasPermission(PermissionCodes.Users.View))
            throw new UnauthorizedAccessException($"Permission denied: {PermissionCodes.Employees.View}");

        return await _employees.ListAsync();
    }

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
        if (code.Length == 0) code = await _employees.GenerateNextCodeAsync();

        if (!errors.ContainsKey("Code") && await _employees.CodeExistsAsync(code))
            errors["Code"] = "DuplicateCode";

        if (request.BranchId is int branchId && !await _employees.BranchIsActiveAsync(branchId))
            errors["BranchId"] = "InvalidBranch";
        if (request.DepartmentId is int departmentId && !await _employees.DepartmentIsActiveAsync(departmentId))
            errors["DepartmentId"] = "InvalidDepartment";

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
                var employee = new Employee(code, request.FullName,
                    request.Phone.Replace(" ", "").Replace("-", ""), request.Email,
                    request.JobTitle, request.BranchId!.Value, request.HireDate!.Value, request.PhotoKey, request.DepartmentId);

                await _employees.AddAsync(employee);
                await _employees.SaveChangesAsync();   // obtain the internal Id
                employeeId = employee.Id;

                if (request.Account is not null)
                {
                    int roleId = await _employees.FindRoleIdAsync(request.Account.RoleName)
                        ?? throw new InvalidOperationException("Role was not found.");

                    var user = new User(request.Account.UserName.Trim(), employee.FullName,
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


    public async Task<Employee?> GetByCodeAsync(string code)
    {
        if (!_permissions.HasPermission(PermissionCodes.Employees.View))
            throw new UnauthorizedAccessException($"Permission denied: {PermissionCodes.Employees.View}");
        return await _employees.GetByCodeAsync(code.Trim().ToUpperInvariant());
    }

    /// <summary>Updates master data. Validation problems are returned in Errors; permission and lookup problems throw.</summary>
    public async Task<CreateEmployeeResult> UpdateAsync(string currentCode, CreateEmployeeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new InvalidOperationException("A signed-in user is required.");
        _permissions.DemandPermission(PermissionCodes.Employees.Edit);
        if (request.Account is not null)
            throw new InvalidOperationException("Login accounts must be managed separately.");

        var employee = await _employees.GetByCodeAsync(currentCode.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Employee was not found.");
        var errors = Validate(request);
        var newCode = string.IsNullOrWhiteSpace(request.Code) ? employee.Code : request.Code.Trim().ToUpperInvariant();
        if (!string.Equals(newCode, employee.Code, StringComparison.OrdinalIgnoreCase) &&
            !errors.ContainsKey("Code") && await _employees.CodeExistsAsync(newCode))
            errors["Code"] = "DuplicateCode";
        if (request.BranchId is int branchId && !await _employees.BranchIsActiveAsync(branchId))
            errors["BranchId"] = "InvalidBranch";
        if (request.DepartmentId is int departmentId && !await _employees.DepartmentIsActiveAsync(departmentId))
            errors["DepartmentId"] = "InvalidDepartment";
        if (errors.Count > 0) return new CreateEmployeeResult { Success = false, Errors = errors };

        employee.UpdateDetails(newCode, request.FullName,
            request.Phone.Replace(" ", "").Replace("-", ""), request.Email, request.JobTitle, request.BranchId!.Value,
            request.HireDate!.Value, request.PhotoKey ?? employee.PhotoKey, request.DepartmentId);
        await _employees.SaveChangesAsync();
        await _audit.AddAsync(new AuditLog(_session.UserId.Value, "Employee.Update", nameof(Employee),
            employee.Id.ToString(), $"code={employee.Code}; branch={employee.BranchId}", null));
        return new CreateEmployeeResult { Success = true, EmployeeId = employee.Id };
    }

    public async Task SetActiveAsync(string code, bool active)
    {
        if (!_session.IsAuthenticated || _session.UserId is null)
            throw new InvalidOperationException("A signed-in user is required.");
        _permissions.DemandPermission(PermissionCodes.Employees.Deactivate);
        var employee = await _employees.GetByCodeAsync(code.Trim().ToUpperInvariant())
            ?? throw new InvalidOperationException("Employee was not found.");
        if (active) employee.Activate(); else employee.Deactivate();
        await _employees.SaveChangesAsync();
        await _audit.AddAsync(new AuditLog(_session.UserId.Value, active ? "Employee.Activate" : "Employee.Deactivate",
            nameof(Employee), employee.Id.ToString(), $"code={employee.Code}; active={active}", null));
    }

    private Dictionary<string, string> Validate(CreateEmployeeRequest r)
    {
        var errors = new Dictionary<string, string>();
        DateOnly today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

        // The code is optional here: when empty, the service generates it. A typed code must be well formed.
        string code = (r.Code ?? string.Empty).Trim();
        if (code.Length > 0 && !CodePattern.IsMatch(code)) errors["Code"] = "InvalidCode";

        ValidateName(errors, r.FullName);

        string phone = (r.Phone ?? string.Empty).Replace(" ", "").Replace("-", "");
        if (phone.Length == 0) errors["Phone"] = "Required";
        else if (!PhonePattern.IsMatch(phone)) errors["Phone"] = "InvalidPhone";

        string email = (r.Email ?? string.Empty).Trim();
        if (email.Length == 0) errors["Email"] = "Required";
        else if (email.Length > 200 || !EmailPattern.IsMatch(email)) errors["Email"] = "InvalidEmail";

        if (string.IsNullOrWhiteSpace(r.JobTitle)) errors["JobTitle"] = "Required";
        else if (!JobTitles.IsValid(r.JobTitle)) errors["JobTitle"] = "InvalidJob";

        if (r.BranchId is null) errors["BranchId"] = "Required";

        if (r.HireDate is null) errors["HireDate"] = "Required";
        else if (r.HireDate > today) errors["HireDate"] = "FutureDate";

        if (r.Account is not null)
        {
            if (!UserNamePattern.IsMatch((r.Account.UserName ?? string.Empty).Trim())) errors["UserName"] = "InvalidUserName";
            if ((r.Account.Password ?? string.Empty).Length < 8) errors["Password"] = "WeakPassword";
            if (string.IsNullOrWhiteSpace(r.Account.RoleName)) errors["RoleName"] = "Required";
        }

        return errors;
    }

    private static void ValidateName(Dictionary<string, string> errors, string? value)
    {
        string name = (value ?? string.Empty).Trim();
        if (name.Length == 0) { errors["FullName"] = "Required"; return; }
        if (name.Length > 200 || !NamePattern.IsMatch(name)) { errors["FullName"] = "InvalidCharacters"; return; }
        if (name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length < 2) errors["FullName"] = "FullNameWords";
    }

    private static CreateEmployeeResult Fail(IReadOnlyDictionary<string, string> errors) =>
        new() { Success = false, Errors = errors };
}
