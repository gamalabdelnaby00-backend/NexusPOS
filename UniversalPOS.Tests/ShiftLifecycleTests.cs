using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.CashManagement;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Sales;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Tests;

public class ShiftLifecycleTests
{
    private const int CashierId = 7;
    private const int OtherUserId = 8;
    private const int ManagerId = 9;
    private const int TerminalId = 1;

    private static CurrentUserSession SignedIn(int userId, string[] roles, string[] permissions)
    {
        var session = new CurrentUserSession();
        session.SignIn(userId, $"user{userId}", $"User {userId}", roles, permissions);
        return session;
    }

    // ---------- Creating a sale ----------

    [Fact]
    public async Task CreateSale_WithVerifiedOpenShift_Succeeds()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        var session = SignedIn(CashierId, new[] { "Cashier" }, Array.Empty<string>());
        session.SetActiveShift(shift.Id, TerminalId);
        var sales = new FakeSaleRepository();

        int saleId = await new CreateSaleService(sales, session, shifts).ExecuteAsync();

        Assert.True(saleId > 0);
        Assert.Equal(shift.Id, sales.SavedSale!.CashShiftId);
        Assert.Equal(CashierId, sales.SavedSale.UserId);
    }

    [Fact]
    public async Task CreateSale_WithoutShiftInSession_ThrowsShiftRequired()
    {
        var session = SignedIn(CashierId, new[] { "Cashier" }, Array.Empty<string>());
        var service = new CreateSaleService(new FakeSaleRepository(), session, new FakeCashShiftRepository());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync());

        Assert.Equal(ServiceErrors.ShiftRequired, ServiceErrors.GetCode(ex));
    }

    [Fact]
    public async Task CreateSale_WhenShiftIsClosedInDatabase_IsRejected()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        shift.RequestClose(100m, 100m);
        var session = SignedIn(CashierId, new[] { "Cashier" }, Array.Empty<string>());
        session.SetActiveShift(shift.Id, TerminalId);
        var sales = new FakeSaleRepository();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CreateSaleService(sales, session, shifts).ExecuteAsync());

        Assert.Equal(ServiceErrors.ShiftNotOpen, ServiceErrors.GetCode(ex));
        Assert.Null(sales.SavedSale);
    }

    [Fact]
    public async Task CreateSale_WhenShiftBelongsToAnotherUser_IsRejected()
    {
        var shifts = new FakeCashShiftRepository();
        var othersShift = shifts.Seed(new CashShift(OtherUserId, TerminalId, 100m));
        var session = SignedIn(CashierId, new[] { "Cashier" }, Array.Empty<string>());
        session.SetActiveShift(othersShift.Id, TerminalId);
        var sales = new FakeSaleRepository();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CreateSaleService(sales, session, shifts).ExecuteAsync());

        Assert.Equal(ServiceErrors.ShiftNotOwned, ServiceErrors.GetCode(ex));
        Assert.Null(sales.SavedSale);
    }

    // ---------- Restoring the session shift ----------

    [Fact]
    public async Task RestoreShift_ResumesOwnOpenShift_WithoutCreatingOne()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 50m));
        var session = SignedIn(CashierId, new[] { "Cashier" }, Array.Empty<string>());

        int? restored = await new ActiveShiftService(session, shifts).RestoreAsync();

        Assert.Equal(shift.Id, restored);
        Assert.Equal(shift.Id, session.CashShiftId);
        Assert.Equal(TerminalId, session.TerminalId);
    }

    [Fact]
    public async Task RestoreShift_DropsStaleSessionShiftOfAnotherUser()
    {
        var shifts = new FakeCashShiftRepository();
        var othersShift = shifts.Seed(new CashShift(OtherUserId, TerminalId, 50m));
        var session = SignedIn(CashierId, new[] { "Cashier" }, Array.Empty<string>());
        session.SetActiveShift(othersShift.Id, TerminalId);

        int? restored = await new ActiveShiftService(session, shifts).RestoreAsync();

        Assert.Null(restored);
        Assert.Null(session.CashShiftId);
    }

    [Fact]
    public async Task RestoreShift_ForUserWithoutShift_ReturnsNullAndCreatesNothing()
    {
        var shifts = new FakeCashShiftRepository();
        var session = SignedIn(CashierId, new[] { "Admin" }, Array.Empty<string>());

        int? restored = await new ActiveShiftService(session, shifts).RestoreAsync();

        Assert.Null(restored);
        Assert.False(await shifts.HasActiveShiftForUserAsync(CashierId));
    }

    // ---------- Cash sale records drawer movement ----------

    private static Sale PaidSale(int shiftId, PaymentMethod method, decimal quantity = 2m)
    {
        var sale = new Sale();
        sale.AssignContext(CashierId, shiftId, TerminalId, Guid.NewGuid());
        sale.AddItem(new SaleItem(1, 100m, quantity));
        sale.AddPayment(new Payment(sale.TotalAmount, method));
        return sale;
    }

    private static async Task<(FakeSaleRepository sales, FakeStockRepository stock)> SalesWithStock()
    {
        var sales = new FakeSaleRepository();
        var stock = new FakeStockRepository();
        stock.AddProduct(1);
        await stock.AddBalanceAsync(new StockBalance(1, 10m));
        return (sales, stock);
    }

    [Fact]
    public async Task CompleteSale_CashPayment_RecordsOneCashSaleMovement()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        var (sales, stock) = await SalesWithStock();
        var sale = PaidSale(shift.Id, PaymentMethod.Cash);
        await sales.AddAsync(sale);

        await new CompleteSaleService(sales, stock, new FakeUnitOfWork(), shifts).ExecuteAsync(sale.Id);

        var movement = Assert.Single(shifts.Movements);
        Assert.Equal(CashMovementType.CashSale, movement.Type);
        Assert.Equal(200m, movement.Amount);
        Assert.True(movement.IncreasesDrawer);
        Assert.Equal(shift.Id, movement.CashShiftId);
    }

    [Fact]
    public async Task CompleteSale_CardPayment_DoesNotTouchTheDrawer()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        var (sales, stock) = await SalesWithStock();
        var sale = PaidSale(shift.Id, PaymentMethod.Card);
        await sales.AddAsync(sale);

        await new CompleteSaleService(sales, stock, new FakeUnitOfWork(), shifts).ExecuteAsync(sale.Id);

        Assert.Empty(shifts.Movements);
        Assert.Equal(SaleReturnStatus.Completed, sale.Status);
    }

    [Fact]
    public async Task CompleteSale_WhenShiftWasClosed_FailsWithoutChangingStockOrSale()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        var (sales, stock) = await SalesWithStock();
        var sale = PaidSale(shift.Id, PaymentMethod.Cash);
        await sales.AddAsync(sale);
        shift.RequestClose(100m, 100m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new CompleteSaleService(sales, stock, new FakeUnitOfWork(), shifts).ExecuteAsync(sale.Id));

        Assert.Equal(ServiceErrors.ShiftNotOpen, ServiceErrors.GetCode(ex));
        Assert.Equal(SaleReturnStatus.Draft, sale.Status);
        Assert.Equal(10m, (await stock.GetBalanceAsync(1))!.Quantity);
        Assert.Empty(shifts.Movements);
    }


    // ---------- Selling without a shift (Admin) ----------

    [Fact]
    public async Task CreateSale_AdminWithoutShift_IsAllowedAndNotTiedToADrawer()
    {
        var session = SignedIn(1, new[] { "Admin" }, Array.Empty<string>());
        var sales = new FakeSaleRepository();
        var service = new CreateSaleService(sales, session, new FakeCashShiftRepository(), new PermissionChecker(session));

        int saleId = await service.ExecuteAsync();

        Assert.True(saleId > 0);
        Assert.Null(sales.SavedSale!.CashShiftId);
        Assert.Equal(1, sales.SavedSale.UserId);
    }

    [Fact]
    public async Task CreateSale_CashierWithoutShift_IsStillRejectedEvenWithChecker()
    {
        var session = SignedIn(CashierId, new[] { "Cashier" }, new[] { "Sales.Create" });
        var service = new CreateSaleService(new FakeSaleRepository(), session, new FakeCashShiftRepository(), new PermissionChecker(session));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync());

        Assert.Equal(ServiceErrors.ShiftRequired, ServiceErrors.GetCode(ex));
    }

    [Fact]
    public async Task CompleteSale_WithoutShift_CompletesAndRecordsNoCashMovement()
    {
        var shifts = new FakeCashShiftRepository();
        var (sales, stock) = await SalesWithStock();
        var sale = new Sale();
        sale.AssignContextWithoutShift(1, Guid.NewGuid());
        sale.AddItem(new SaleItem(1, 100m, 1m));
        sale.AddPayment(new Payment(100m, PaymentMethod.Cash));
        await sales.AddAsync(sale);

        await new CompleteSaleService(sales, stock, new FakeUnitOfWork(), shifts).ExecuteAsync(sale.Id);

        Assert.Equal(SaleReturnStatus.Completed, sale.Status);
        Assert.Empty(shifts.Movements);
        Assert.Equal(9m, (await stock.GetBalanceAsync(1))!.Quantity);
    }

    // ---------- Closing a shift ----------

    private static CloseCashShiftService CloseService(
        FakeCashShiftRepository shifts, CurrentUserSession session, FakeAuditLogRepository? audit = null) =>
        new(shifts, audit ?? new FakeAuditLogRepository(), new FakeUnitOfWork(), session, new PermissionChecker(session));

    [Fact]
    public async Task CloseShift_ExpectedCashIsOpeningFloatPlusSalesMinusRefunds()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        await shifts.AddCashMovementAsync(new CashMovement(shift.Id, CashMovementType.CashSale, 300m, true, "sale", CashierId));
        await shifts.AddCashMovementAsync(new CashMovement(shift.Id, CashMovementType.CashRefund, 50m, false, "refund", CashierId));
        var session = SignedIn(CashierId, new[] { "Cashier" }, new[] { "Shifts.Close" });
        session.SetActiveShift(shift.Id, TerminalId);

        var result = await CloseService(shifts, session).ExecuteAsync(shift.Id, countedCash: 350m);

        Assert.Equal(350m, result.ExpectedCash);   // 100 + 300 - 50
        Assert.Equal(0m, result.Difference);
        Assert.Equal(CashShiftStatus.Closed, result.Status);
        Assert.Null(session.CashShiftId);
    }

    [Fact]
    public async Task CloseShift_WithShortage_WaitsForApprovalAndIsAudited()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        var session = SignedIn(CashierId, new[] { "Cashier" }, new[] { "Shifts.Close" });
        var audit = new FakeAuditLogRepository();

        var result = await CloseService(shifts, session, audit).ExecuteAsync(shift.Id, countedCash: 90m);

        Assert.Equal(-10m, result.Difference);
        Assert.Equal(CashShiftStatus.PendingApproval, result.Status);
        Assert.Contains(audit.Entries, e => e.Action == "Shift.Close");
    }

    [Fact]
    public async Task CloseShift_OfAnotherUser_RequiresReconcilePermission()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        var noPermission = SignedIn(OtherUserId, new[] { "Cashier" }, new[] { "Shifts.Close" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CloseService(shifts, noPermission).ExecuteAsync(shift.Id, 100m));
        Assert.Equal(CashShiftStatus.Open, shift.Status);

        var manager = SignedIn(ManagerId, new[] { "Manager" }, new[] { "Shifts.Reconcile" });
        var result = await CloseService(shifts, manager).ExecuteAsync(shift.Id, 100m);
        Assert.Equal(CashShiftStatus.Closed, result.Status);
    }

    [Fact]
    public async Task CloseShift_Twice_IsRejected()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        var session = SignedIn(CashierId, new[] { "Cashier" }, new[] { "Shifts.Close" });
        var service = CloseService(shifts, session);

        await service.ExecuteAsync(shift.Id, 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(shift.Id, 100m));
    }

    // ---------- Approving a difference ----------

    private static ApproveShiftDifferenceService ApproveService(
        FakeCashShiftRepository shifts, CurrentUserSession session) =>
        new(shifts, new FakeAuditLogRepository(), new FakeUnitOfWork(), session, new PermissionChecker(session));

    private static CashShift PendingShift(FakeCashShiftRepository shifts)
    {
        var shift = shifts.Seed(new CashShift(CashierId, TerminalId, 100m));
        shift.RequestClose(100m, 80m);
        return shift;
    }

    [Fact]
    public async Task ApproveDifference_ByManager_ClosesShiftAndRecordsApprover()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = PendingShift(shifts);
        var manager = SignedIn(ManagerId, new[] { "Manager" }, new[] { "Shifts.ApproveDifference" });

        await ApproveService(shifts, manager).ExecuteAsync(shift.Id);

        Assert.Equal(CashShiftStatus.Closed, shift.Status);
        Assert.Equal(ManagerId, shift.DifferenceApprovedByUserId);
    }

    [Fact]
    public async Task ApproveDifference_ByAccountantWithoutPermission_IsDenied()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = PendingShift(shifts);
        var accountant = SignedIn(10, new[] { "Accountant" }, new[] { "Shifts.ViewAll", "Reports.Shifts" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ApproveService(shifts, accountant).ExecuteAsync(shift.Id));

        Assert.Equal(CashShiftStatus.PendingApproval, shift.Status);
    }

    [Fact]
    public async Task ApproveDifference_OwnShift_IsRejectedEvenWithPermission()
    {
        var shifts = new FakeCashShiftRepository();
        var shift = PendingShift(shifts);
        var owner = SignedIn(CashierId, new[] { "Manager" }, new[] { "Shifts.ApproveDifference" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => ApproveService(shifts, owner).ExecuteAsync(shift.Id));

        Assert.Equal(CashShiftStatus.PendingApproval, shift.Status);
    }
}
