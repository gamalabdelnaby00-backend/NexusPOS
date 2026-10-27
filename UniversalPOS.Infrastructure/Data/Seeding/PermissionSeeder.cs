
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Infrastructure.Data.Seeding;

public sealed class PermissionSeeder
{
    private readonly AppDbContext _db;

    public PermissionSeeder(AppDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        var existingCodes = await _db.Permissions
            .Select(p => p.Code)
            .ToListAsync();

        var codeSet = new HashSet<string>(
            existingCodes,
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in PermissionCodes.All)
        {
            if (!codeSet.Add(item.Code))
                continue;

            _db.Permissions.Add(
                new Permission(item.Code, item.Name, item.Module));
        }

        await _db.SaveChangesAsync();

        var roleDefinitions = new[]
        {
            ("Admin", "System administrator"),
            ("Manager", "Branch management, staff, purchasing and reports"),
            ("Cashier", "Sales and cashier shifts"),
            ("Accountant", "Purchases and financial reports"),
            ("Inventory", "Products and inventory"),
            ("Supervisor", "Approval of sensitive operations")
        };

        var existingRoleNames = await _db.Roles
            .Select(r => r.Name)
            .ToListAsync();

        var roleNameSet = new HashSet<string>(
            existingRoleNames,
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in roleDefinitions)
        {
            if (!roleNameSet.Add(item.Item1))
                continue;

            _db.Roles.Add(
                new Role(item.Item1, item.Item2, isSystemRole: true));
        }

        await _db.SaveChangesAsync();

        var permissions = await _db.Permissions
            .Where(p => p.IsActive)
            .ToListAsync();

        var roles = await _db.Roles
            .Where(r => r.IsActive)
            .ToListAsync();

        var existingLinks = await _db.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync();

        var linkSet = new HashSet<string>(
            existingLinks.Select(x => $"{x.RoleId}:{x.PermissionId}"));

        var cashier = new[]
        {
            "Sales.View", "Sales.Create", "Sales.AddItems",
            "Sales.ChangeQuantity", "Sales.ApplyDiscount",
            "Sales.RegisterPayment", "Sales.RegisterSplitPayment",
            "Sales.Complete", "Sales.Cancel", "Sales.ViewDetails",
            "Sales.ReprintReceipt", "Returns.View", "Returns.Create",
            "Shifts.ViewOwn", "Shifts.Open", "Shifts.Close",
            "Shifts.CountCash", "CashMovements.ViewOwn",
            "Products.View", "Categories.View", "Inventory.ViewBalances"
        };

        var accountant = new[]
        {
            "Suppliers.View", "Suppliers.Create", "Suppliers.Edit",
            "Purchases.View", "Purchases.Create", "Purchases.EditDraft",
            "Purchases.Receive", "Purchases.Return",
            "SupplierPayments.View", "SupplierPayments.Register",
            "SupplierBalances.View", "Reports.Sales", "Reports.Payments",
            "Reports.Purchases", "Reports.SupplierBalances",
            // Read-only visibility over shifts, cash, inventory value and reports.
            "Sales.View", "Sales.ViewDetails", "Products.View", "Categories.View",
            "Shifts.ViewAll", "Shifts.ViewExpectedCash", "CashMovements.ViewAll",
            "Inventory.ViewBalances", "Inventory.ViewMovements", "Inventory.ViewCost",
            "Inventory.ViewValuation", "Reports.Returns", "Reports.Shifts",
            "Reports.Inventory"
        };

        var inventory = new[]
        {
            "Products.View", "Products.Create", "Products.Edit",
            "Products.ManageBarcode", "Categories.View",
            "Categories.Create", "Categories.Edit", "Units.View",
            "Units.Manage", "Inventory.ViewBalances",
            "Inventory.ViewMovements", "Inventory.ReceiveStock",
            "Inventory.AdjustStock", "Inventory.CountStock",
            "Inventory.Export"
        };

        var supervisor = new[]
        {
            "Sales.View", "Sales.ApplyHighDiscount",
            "Sales.CancelCompleted", "Returns.View", "Returns.Approve",
            "Returns.RefundCash", "Returns.RefundToOriginalMethod",
            "Shifts.ViewAll", "Shifts.Reconcile",
            "Shifts.ApproveDifference", "CashMovements.ViewAll",
            "Inventory.ApproveAdjustment", "Inventory.ApproveCount",
            "AuditLogs.View", "Reports.Sales", "Reports.Returns",
            "Reports.Shifts"
        };

        var manager = new[]
        {
            // Everything a Supervisor and an Inventory clerk can do...
            "Sales.View", "Sales.ViewDetails", "Sales.ApplyHighDiscount",
            "Sales.CancelCompleted", "Sales.ReprintReceipt",
            "Returns.View", "Returns.ViewHistory", "Returns.Approve",
            "Returns.RefundCash", "Returns.RefundToOriginalMethod",
            "Shifts.ViewAll", "Shifts.ViewExpectedCash", "Shifts.Reconcile",
            "Shifts.ApproveDifference", "CashMovements.ViewAll",
            "Products.View", "Products.Create", "Products.Edit",
            "Products.Deactivate", "Products.ChangePrice", "Products.ChangeCost",
            "Products.ManageBarcode", "Categories.View", "Categories.Create",
            "Categories.Edit", "Categories.Deactivate", "Units.View", "Units.Manage",
            "Inventory.ViewBalances", "Inventory.ViewMovements",
            "Inventory.ReceiveStock", "Inventory.AdjustStock",
            "Inventory.ApproveAdjustment", "Inventory.CountStock",
            "Inventory.ApproveCount", "Inventory.TransferStock",
            "Inventory.ViewCost", "Inventory.ViewValuation", "Inventory.Export",
            "AuditLogs.View",
            // ...plus staff, purchasing, settings and reports. Never admin accounts (see RoleHierarchy).
            "Users.View", "Users.Create", "Users.Edit", "Users.Deactivate",
            "Users.ResetPassword", "Users.AssignRoles", "Users.ViewPermissions",
            "Roles.View", "Permissions.View",
            "Employees.View", "Employees.Create", "Employees.Edit", "Employees.Deactivate",
            "Suppliers.View", "Suppliers.Create", "Suppliers.Edit", "Suppliers.Deactivate",
            "Purchases.View", "Purchases.Create", "Purchases.EditDraft",
            "Purchases.Receive", "Purchases.Cancel", "Purchases.Return",
            "SupplierPayments.View", "SupplierPayments.Register",
            "SupplierBalances.View", "Branches.View",
            "Settings.View", "Settings.Manage",
            "Reports.Sales", "Reports.Payments", "Reports.Returns", "Reports.Shifts",
            "Reports.Inventory", "Reports.Purchases", "Reports.SupplierBalances",
            "Reports.Profit", "Reports.Export"
        };

        var grants = new Dictionary<string, string[]>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Manager"] = manager,
            ["Cashier"] = cashier,
            ["Accountant"] = accountant,
            ["Inventory"] = inventory,
            ["Supervisor"] = supervisor
        };

        var permissionByCode = permissions.ToDictionary(
            p => p.Code,
            StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            IEnumerable<Permission> allowed;

            if (role.Name.Equals("Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Automatically includes future catalog entries after seeding.
                allowed = permissions;
            }
            else if (grants.TryGetValue(role.Name, out var codes))
            {
                allowed = codes
                    .Where(permissionByCode.ContainsKey)
                    .Select(code => permissionByCode[code]);
            }
            else
            {
                continue;
            }

            foreach (var permission in allowed)
            {
                var key = $"{role.Id}:{permission.Id}";

                if (!linkSet.Add(key))
                    continue;

                _db.RolePermissions.Add(
                    new RolePermission(role.Id, permission.Id));
            }
        }

        await _db.SaveChangesAsync();
    }
}
