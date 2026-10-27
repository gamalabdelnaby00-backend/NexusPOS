using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Common;
using UniversalPOS.Application.Inventory;
using UniversalPOS.Application.Sales;
using UniversalPOS.Application.Security;
using UniversalPOS.Application.Settings;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Tests;

public class PricingAndStockTests
{
    // ---------- Stock indicator thresholds ----------

    [Theory]
    [InlineData(0, StockLevel.OutOfStock)]
    [InlineData(1, StockLevel.Critical)]
    [InlineData(9.999, StockLevel.Critical)]
    [InlineData(10, StockLevel.Low)]
    [InlineData(20, StockLevel.Low)]
    [InlineData(20.001, StockLevel.Good)]
    [InlineData(500, StockLevel.Good)]
    public void StockStatus_UsesAgreedThresholds(double quantity, StockLevel expected)
    {
        Assert.Equal(expected, StockStatus.Classify((decimal)quantity));
    }

    [Fact]
    public void StockStatus_ZeroCannotBeSold_AndStatusHasText()
    {
        Assert.False(StockStatus.CanSell(0m));
        Assert.True(StockStatus.CanSell(1m));
        Assert.False(string.IsNullOrWhiteSpace(StockStatus.ArabicText(StockLevel.Critical)));
    }

    // ---------- Product ----------

    [Fact]
    public void Product_CodeDefaultsToBarcode_AndCostMustNotBeNegative()
    {
        var product = new Product("Panadol", 30m, "6221234", 1, costPrice: 20m, minStock: 5m);

        Assert.Equal("6221234", product.Code);
        Assert.Equal(20m, product.CostPrice);
        Assert.Equal(5m, product.MinStock);
        Assert.Throws<ArgumentException>(() => new Product("X", 1m, "B", 1, costPrice: -1m));
    }

    // ---------- Sale totals ----------

    private static Sale SaleOf(decimal unitPrice, decimal qty)
    {
        var sale = new Sale();
        sale.AssignContext(1, 1, 1, Guid.NewGuid());
        sale.AddItem(new SaleItem(1, unitPrice, qty));
        return sale;
    }

    [Fact]
    public void Sale_DefaultsToNoDiscountAndNoTax()
    {
        var sale = SaleOf(100m, 2m);

        Assert.Equal(200m, sale.TotalAmount);
        Assert.Equal(0m, sale.DiscountAmount);
        Assert.Equal(0m, sale.TaxAmount);
    }

    [Fact]
    public void Sale_Discount_ReducesTotal_AndCannotExceedSubtotal()
    {
        var sale = SaleOf(100m, 2m);

        sale.ApplyDiscount(50m, appliedByUserId: 1);
        Assert.Equal(150m, sale.TotalAmount);
        Assert.Equal(1, sale.DiscountAppliedByUserId);

        Assert.Throws<InvalidOperationException>(() => sale.ApplyDiscount(250m, 1));
    }

    [Fact]
    public void Sale_InclusiveTax_ExtractsTaxWithoutChangingTotal()
    {
        var sale = SaleOf(114m, 1m);

        sale.ApplyTax("VAT", 14m, included: true);

        Assert.Equal(114m, sale.TotalAmount);
        Assert.Equal(14m, sale.TaxAmount);
    }

    [Fact]
    public void Sale_ExclusiveTax_AddsTaxOnTopOfDiscountedAmount()
    {
        var sale = SaleOf(100m, 1m);
        sale.ApplyDiscount(10m, 1);

        sale.ApplyTax("VAT", 10m, included: false);

        Assert.Equal(9m, sale.TaxAmount);       // 10% of 90
        Assert.Equal(99m, sale.TotalAmount);
    }

    [Fact]
    public void Sale_DiscountAndTaxLocked_AfterPaymentRegistered()
    {
        var sale = SaleOf(100m, 1m);
        sale.AddPayment(new Payment(100m, PaymentMethod.Cash));

        Assert.Throws<InvalidOperationException>(() => sale.ApplyDiscount(10m, 1));
        Assert.Throws<InvalidOperationException>(() => sale.ApplyTax("VAT", 14m, true));
    }

    [Theory]
    [InlineData("01012345678", true)]
    [InlineData("+20 100-123-4567", true)]
    [InlineData("12", false)]
    [InlineData("abc12345678", false)]
    public void Sale_CustomerPhone_IsValidated(string phone, bool valid)
    {
        var sale = SaleOf(10m, 1m);

        if (valid) { sale.SetCustomerPhone(phone); Assert.Equal(phone.Trim(), sale.CustomerPhone); }
        else Assert.Throws<ArgumentException>(() => sale.SetCustomerPhone(phone));
    }

    [Fact]
    public void Sale_CustomerPhone_IsOptional()
    {
        var sale = SaleOf(10m, 1m);
        sale.SetCustomerPhone("");
        Assert.Null(sale.CustomerPhone);
    }

    // ---------- Discount service ----------

    private static (SaleAdjustmentService service, FakeSaleRepository sales, Sale sale, FakeAuditLogRepository audit)
        Build(string[] roles, string[] permissions, int userId = 1)
    {
        var session = new CurrentUserSession();
        session.SignIn(userId, "u", "U", roles, permissions);
        var sales = new FakeSaleRepository();
        var sale = new Sale();
        sale.AssignContext(userId, 1, 1, Guid.NewGuid());
        sale.AddItem(new SaleItem(1, 100m, 1m));
        sales.AddAsync(sale).GetAwaiter().GetResult();
        var audit = new FakeAuditLogRepository();
        var service = new SaleAdjustmentService(sales, new FakeSystemSettingsRepository(), audit,
            new FakeUnitOfWork(), session, new PermissionChecker(session));
        return (service, sales, sale, audit);
    }

    [Fact]
    public async Task Discount_WithinCashierLimit_IsAllowedAndAudited()
    {
        var (service, _, sale, audit) = Build(new[] { "Cashier" }, new[] { "Sales.ApplyDiscount" });

        await service.ApplyDiscountAsync(sale.Id, DiscountKind.Percent, 10m);   // limit is 10%

        Assert.Equal(10m, sale.DiscountAmount);
        Assert.Contains(audit.Entries, e => e.Action == "Sale.Discount" && e.UserId == 1);
    }

    [Fact]
    public async Task Discount_AboveCashierLimit_NeedsHighDiscountPermission()
    {
        var (service, _, sale, _) = Build(new[] { "Cashier" }, new[] { "Sales.ApplyDiscount" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyDiscountAsync(sale.Id, DiscountKind.Percent, 25m));

        Assert.Equal(ServiceErrors.PermissionDenied, ServiceErrors.GetCode(ex));
        Assert.Equal(0m, sale.DiscountAmount);
    }

    [Fact]
    public async Task Discount_AboveLimit_AllowedForManager()
    {
        var (service, _, sale, _) = Build(new[] { "Manager" }, new[] { "Sales.ApplyHighDiscount" });

        await service.ApplyDiscountAsync(sale.Id, DiscountKind.Amount, 40m);

        Assert.Equal(40m, sale.DiscountAmount);
    }

    [Fact]
    public async Task Discount_WithoutAnyDiscountPermission_IsDenied()
    {
        var (service, _, sale, _) = Build(new[] { "Cashier" }, Array.Empty<string>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyDiscountAsync(sale.Id, DiscountKind.Amount, 1m));
    }

    [Fact]
    public async Task Discount_OnAnotherUsersSale_IsRejected()
    {
        var (service, _, sale, _) = Build(new[] { "Cashier" }, new[] { "Sales.ApplyDiscount" });
        var session = new CurrentUserSession();
        session.SignIn(2, "other", "Other", new[] { "Cashier" }, new[] { "Sales.ApplyDiscount" });
        var sales = new FakeSaleRepository();
        await sales.AddAsync(sale);
        var otherService = new SaleAdjustmentService(sales, new FakeSystemSettingsRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), session, new PermissionChecker(session));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => otherService.ApplyDiscountAsync(sale.Id, DiscountKind.Amount, 1m));
    }

    [Fact]
    public async Task ConfiguredTax_IsOffByDefault_AndAppliedOnlyWhenEnabled()
    {
        var (service, _, sale, _) = Build(new[] { "Cashier" }, Array.Empty<string>());

        await service.ApplyConfiguredTaxAsync(sale.Id);
        Assert.Null(sale.TaxRate);
        Assert.Equal(100m, sale.TotalAmount);
    }

    // ---------- Settings permissions ----------

    private static SettingsService SettingsFor(string[] roles, string[] permissions, FakeSystemSettingsRepository repo)
    {
        var session = new CurrentUserSession();
        session.SignIn(1, "u", "U", roles, permissions);
        return new SettingsService(repo, new FakeAuditLogRepository(), new FakeUnitOfWork(),
            session, new PermissionChecker(session));
    }

    [Fact]
    public async Task Tax_CanBeEnabledOnlyWithSettingsPermission()
    {
        var repo = new FakeSystemSettingsRepository();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => SettingsFor(new[] { "Cashier" }, Array.Empty<string>(), repo).ConfigureTaxAsync(true, "VAT", 14m, true));
        Assert.False(repo.Settings.TaxEnabled);

        await SettingsFor(new[] { "Manager" }, new[] { "Settings.Manage" }, repo).ConfigureTaxAsync(true, "VAT", 14m, true);
        Assert.True(repo.Settings.TaxEnabled);
        Assert.Equal(14m, repo.Settings.TaxRate);
    }
}
