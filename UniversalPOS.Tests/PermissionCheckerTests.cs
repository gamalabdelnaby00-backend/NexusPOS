
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Security;
using Xunit;

namespace UniversalPOS.Tests;

public sealed class PermissionCheckerTests
{
    [Fact]
    public void HasPermission_WhenUserHasPermission_ReturnsTrue()
    {
        var session = CreateSession(
            new[] { "Cashier" },
            new[] { "Sales.Create" });

        var checker = new PermissionChecker(session);

        Assert.True(checker.HasPermission("Sales.Create"));
    }

    [Fact]
    public void HasPermission_WhenUserLacksPermission_ReturnsFalse()
    {
        var session = CreateSession(
            new[] { "Cashier" },
            new[] { "Sales.Create" });

        var checker = new PermissionChecker(session);

        Assert.False(checker.HasPermission("Users.Create"));
    }

    [Fact]
    public void HasPermission_WhenUserIsNotAuthenticated_ReturnsFalse()
    {
        var session = new CurrentUserSession();
        var checker = new PermissionChecker(session);

        Assert.False(checker.HasPermission("Sales.Create"));
    }

    [Fact]
    public void HasPermission_WhenUserIsAdmin_ReturnsTrueForFuturePermission()
    {
        var session = CreateSession(
            new[] { "Admin" },
            Array.Empty<string>());

        var checker = new PermissionChecker(session);

        Assert.True(
            checker.HasPermission("FutureModule.NewPermission"));
    }

    [Fact]
    public void HasPermission_IsCaseInsensitive()
    {
        var session = CreateSession(
            new[] { "Cashier" },
            new[] { "Sales.Create" });

        var checker = new PermissionChecker(session);

        Assert.True(checker.HasPermission("sales.create"));
    }

    [Fact]
    public void DemandPermission_WhenPermissionIsMissing_Throws()
    {
        var session = CreateSession(
            new[] { "Cashier" },
            Array.Empty<string>());

        var checker = new PermissionChecker(session);

        Assert.Throws<UnauthorizedAccessException>(
            () => checker.DemandPermission("Users.Create"));
    }

    private static CurrentUserSession CreateSession(
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var session = new CurrentUserSession();

        session.SignIn(
            1,
            "test.user",
            "Test User",
            roles,
            permissions);

        return session;
    }
}
