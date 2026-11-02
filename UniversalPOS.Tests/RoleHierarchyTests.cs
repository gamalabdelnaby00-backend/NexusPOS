using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Tests;

public class RoleHierarchyTests
{
    [Fact]
    public void Manager_CannotManageAdminAccounts()
    {
        Assert.False(RoleHierarchy.CanManageAccount(new[] { "Manager" }, new[] { "Admin" }));
    }

    [Fact]
    public void Manager_CannotManageAnotherManager()
    {
        Assert.False(RoleHierarchy.CanManageAccount(new[] { "Manager" }, new[] { "Manager" }));
    }

    [Fact]
    public void Manager_CanManageLowerRoles()
    {
        Assert.True(RoleHierarchy.CanManageAccount(new[] { "Manager" }, new[] { "Cashier", "Accountant" }));
    }

    [Fact]
    public void Admin_CanManageEveryone()
    {
        Assert.True(RoleHierarchy.CanManageAccount(new[] { "Admin" }, new[] { "Admin" }));
        Assert.True(RoleHierarchy.CanManageAccount(new[] { "Admin" }, new[] { "Manager" }));
    }

    [Fact]
    public void Cashier_CannotManageAnyone()
    {
        Assert.False(RoleHierarchy.CanManageAccount(new[] { "Cashier" }, new[] { "Cashier" }));
    }

    [Fact]
    public void Manager_CannotGrantAdminOrManagerRoles_ButCanGrantLowerOnes()
    {
        Assert.False(RoleHierarchy.CanAssignRole(new[] { "Manager" }, "Admin"));
        Assert.False(RoleHierarchy.CanAssignRole(new[] { "Manager" }, "Manager"));
        Assert.True(RoleHierarchy.CanAssignRole(new[] { "Manager" }, "Cashier"));
    }

    [Fact]
    public void UserWithNoRoles_CanManageNobody()
    {
        Assert.False(RoleHierarchy.CanManageAccount(Array.Empty<string>(), new[] { "Cashier" }));
    }

    private static CurrentUserSession Session(string[] roles, string[] permissions)
    {
        var session = new CurrentUserSession();
        session.SignIn(1, "u", "U", roles, permissions);
        return session;
    }

    [Fact]
    public void StartScreen_Admin_GoesToMain()
    {
        Assert.Equal(StartScreen.Main, StartScreenResolver.Resolve(Session(new[] { "Admin" }, Array.Empty<string>())));
    }

    [Fact]
    public void StartScreen_CashierWithSalesAndShiftPermissions_GoesToOpenShift()
    {
        var session = Session(new[] { "Cashier" }, new[] { "Sales.Create", "Shifts.Open" });
        Assert.Equal(StartScreen.OpenShift, StartScreenResolver.Resolve(session));
    }

    [Fact]
    public void StartScreen_AccountantAndManager_GoToMain_NotUnsupported()
    {
        Assert.Equal(StartScreen.Main, StartScreenResolver.Resolve(Session(new[] { "Accountant" }, new[] { "Reports.Sales" })));
        Assert.Equal(StartScreen.Main, StartScreenResolver.Resolve(Session(new[] { "Manager" }, new[] { "Users.View" })));
    }

    [Fact]
    public void StartScreen_AccountWithoutRoles_IsNone()
    {
        Assert.Equal(StartScreen.None, StartScreenResolver.Resolve(Session(Array.Empty<string>(), Array.Empty<string>())));
    }

    [Fact]
    public void Admin_CanGrantPermissionToAnyRole()
    {
        Assert.True(RoleHierarchy.CanGrantPermission(
            new[] { "Admin" }, Array.Empty<string>(), "Manager", "Users.Manage"));
        Assert.True(RoleHierarchy.CanGrantPermission(
            new[] { "Admin" }, Array.Empty<string>(), "Admin", "Unknown.Permission"));
    }

    [Fact]
    public void Manager_CanGrantOnlyPermissionsTheyHoldToLowerRoles()
    {
        var roles = new[] { "Manager" };
        var permissions = new[] { "Products.View", "Inventory.Adjust" };

        Assert.True(RoleHierarchy.CanGrantPermission(roles, permissions, "Cashier", "Products.View"));
        Assert.False(RoleHierarchy.CanGrantPermission(roles, permissions, "Cashier", "Users.Manage"));
        Assert.False(RoleHierarchy.CanGrantPermission(roles, permissions, "Manager", "Products.View"));
        Assert.False(RoleHierarchy.CanGrantPermission(roles, permissions, "Admin", "Products.View"));
    }

    [Fact]
    public void PermissionGrantChecks_AreCaseInsensitive_AndRejectBlankInputs()
    {
        Assert.True(RoleHierarchy.CanGrantPermission(
            new[] { "Manager" }, new[] { "products.view" }, "Cashier", "Products.View"));
        Assert.False(RoleHierarchy.CanGrantPermission(
            new[] { "Manager" }, new[] { "Products.View" }, " ", "Products.View"));
        Assert.False(RoleHierarchy.CanGrantPermission(
            new[] { "Manager" }, new[] { "Products.View" }, "Cashier", " "));
    }

}
