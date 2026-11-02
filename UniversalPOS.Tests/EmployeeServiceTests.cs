using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Employees;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Tests;

public class EmployeeServiceTests
{
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static (EmployeeService service, FakeEmployeeRepository repo, FakeAuditLogRepository audit) Build(
        string[] roles, string[] permissions)
    {
        var session = new CurrentUserSession();
        session.SignIn(100, "actor", "Actor", roles, permissions);
        var repo = new FakeEmployeeRepository();
        var audit = new FakeAuditLogRepository();
        var service = new EmployeeService(repo, audit, new FakeUnitOfWork(), session, new PermissionChecker(session), new FixedTime());
        return (service, repo, audit);
    }

    private static readonly string[] ManagerPermissions = { "Employees.Create", "Users.Create", "Employees.View" };

    private static CreateEmployeeRequest Valid(string code = "EMP-001", AccountRequest? account = null) => new(
        code, "أحمد محمد علي", "01012345678", "ahmed@example.com",
        "Pharmacist", 1, new DateOnly(2024, 1, 1), null, account);

    [Fact]
    public async Task Create_ValidEmployee_IsSavedWithNormalizedCode_AndAudited()
    {
        var (service, repo, audit) = Build(new[] { "Manager" }, ManagerPermissions);

        var result = await service.CreateAsync(Valid("emp-001"));

        Assert.True(result.Success);
        var employee = Assert.Single(repo.Employees);
        Assert.Equal("EMP-001", employee.Code);
        Assert.Equal("أحمد محمد علي", employee.FullName);
        Assert.Equal("ahmed@example.com", employee.Email);
        Assert.Contains(audit.Entries, e => e.Action == "Employee.Create");
    }

    [Fact]
    public async Task Create_DuplicateCode_IsRejectedWithFieldError()
    {
        var (service, repo, _) = Build(new[] { "Manager" }, ManagerPermissions);
        await service.CreateAsync(Valid());

        var result = await service.CreateAsync(Valid("emp-001"));

        Assert.False(result.Success);
        Assert.Equal("DuplicateCode", result.Errors["Code"]);
        Assert.Single(repo.Employees);
    }

    [Fact]
    public async Task Create_WhenDatabaseConstraintWinsTheRace_ReturnsDuplicateCodeInsteadOfCrashing()
    {
        var (service, repo, _) = Build(new[] { "Manager" }, ManagerPermissions);
        repo.DuplicateFieldOnSave = "Code";

        var result = await service.CreateAsync(Valid());

        Assert.False(result.Success);
        Assert.Equal("DuplicateCode", result.Errors["Code"]);
    }

    [Fact]
    public async Task Create_WithoutCode_GeneratesSequentialCodes()
    {
        var (service, repo, _) = Build(new[] { "Manager" }, ManagerPermissions);

        await service.CreateAsync(Valid(""));
        await service.CreateAsync(Valid(""));

        Assert.Equal(new[] { "EMP-0001", "EMP-0002" }, repo.Employees.Select(e => e.Code).ToArray());
    }

    [Fact]
    public async Task Create_MissingAndInvalidFields_ReportEachFieldSeparately()
    {
        var (service, repo, _) = Build(new[] { "Manager" }, ManagerPermissions);
        var bad = new CreateEmployeeRequest("", "أحمد", "12", "not-an-email", "", null, null, null, null);

        var result = await service.CreateAsync(bad);

        Assert.False(result.Success);
        Assert.Equal("FullNameWords", result.Errors["FullName"]);
        Assert.Equal("InvalidPhone", result.Errors["Phone"]);
        Assert.Equal("InvalidEmail", result.Errors["Email"]);
        Assert.Equal("Required", result.Errors["JobTitle"]);
        Assert.Equal("Required", result.Errors["BranchId"]);
        Assert.Equal("Required", result.Errors["HireDate"]);
        Assert.DoesNotContain("Code", result.Errors.Keys);   // code is generated, not typed
        Assert.Empty(repo.Employees);
    }

    [Fact]
    public async Task Create_NameWithDigits_IsRejected()
    {
        var (service, _, _) = Build(new[] { "Manager" }, ManagerPermissions);
        var request = Valid() with { FullName = "Ahmed 123 Ali" };

        var result = await service.CreateAsync(request);

        Assert.Equal("InvalidCharacters", result.Errors["FullName"]);
    }

    [Fact]
    public async Task Create_FutureHireDate_IsRejected()
    {
        var (service, _, _) = Build(new[] { "Manager" }, ManagerPermissions);
        var request = Valid() with { HireDate = new DateOnly(2999, 1, 1) };

        var result = await service.CreateAsync(request);

        Assert.Equal("FutureDate", result.Errors["HireDate"]);
    }

    [Fact]
    public async Task Create_WithoutPermission_IsDenied()
    {
        var (service, repo, _) = Build(new[] { "Cashier" }, new[] { "Sales.Create" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(Valid()));

        Assert.Empty(repo.Employees);
    }

    [Fact]
    public async Task Create_WithAccount_CreatesLinkedUserAndRole()
    {
        var (service, repo, _) = Build(new[] { "Manager" }, ManagerPermissions);

        var result = await service.CreateAsync(Valid(account: new AccountRequest("ahmed.ali", "Passw0rd!", "Cashier")));

        Assert.True(result.Success);
        var user = Assert.Single(repo.Users);
        Assert.Equal(repo.Employees[0].Id, user.EmployeeId);
        Assert.NotEqual("Passw0rd!", user.PasswordHash);   // stored hashed
        Assert.True(PasswordHashing.Verify("Passw0rd!", user.PasswordHash));
        Assert.Single(repo.UserRoles);
    }

    [Fact]
    public async Task Create_ManagerCannotGrantAdminRole()
    {
        var (service, repo, _) = Build(new[] { "Manager" }, ManagerPermissions);

        var result = await service.CreateAsync(Valid(account: new AccountRequest("boss", "Passw0rd!", "Admin")));

        Assert.False(result.Success);
        Assert.Equal("RoleNotAllowed", result.Errors["RoleName"]);
        Assert.Empty(repo.Employees);
        Assert.Empty(repo.Users);
    }

    [Fact]
    public async Task Create_AccountNeedsUsersCreatePermission()
    {
        var (service, _, _) = Build(new[] { "Accountant" }, new[] { "Employees.Create" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(Valid(account: new AccountRequest("x.user", "Passw0rd!", "Cashier"))));
    }

    [Fact]
    public async Task AssignableRoles_ForManager_ExcludeAdminAndManager()
    {
        var (service, _, _) = Build(new[] { "Manager" }, ManagerPermissions);

        var roles = await service.GetAssignableRolesAsync();

        Assert.DoesNotContain("Admin", roles);
        Assert.DoesNotContain("Manager", roles);
        Assert.Contains("Cashier", roles);
    }

    [Fact]
    public async Task Create_WeakPasswordAndBadUserName_AreReported()
    {
        var (service, _, _) = Build(new[] { "Admin" }, Array.Empty<string>());

        var result = await service.CreateAsync(Valid(account: new AccountRequest("a b", "short", "Cashier")));

        Assert.Equal("InvalidUserName", result.Errors["UserName"]);
        Assert.Equal("WeakPassword", result.Errors["Password"]);
    }
}
