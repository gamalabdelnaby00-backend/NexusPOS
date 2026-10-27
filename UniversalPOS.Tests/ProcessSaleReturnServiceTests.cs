using System.Reflection;
using UniversalPOS.Application.Abstractions;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.SalesReturns;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using Xunit;

namespace UniversalPOS.Tests;
public sealed class ProcessSaleReturnServiceTests
{
    private const int SaleId = 10;
    private const int SaleItemId = 100;
    private const int ProductId = 5;
    private const int UserId = 1;
    private const int CashShiftId = 20;
    private readonly ReturnTestSaleRepository _sales = new();
    private readonly ReturnTestRepository _returns = new();
    private readonly ReturnTestStockRepository _stock = new();
    private readonly ReturnTestUnitOfWork _unitOfWork = new();
    private readonly CurrentUserSession _session = new();
    private ProcessSaleReturnService CreateService()
    {
        return new ProcessSaleReturnService(_sales,_returns,_stock,_unitOfWork,_session);
    }

    private void SignIn(bool withCashShift = false)
    {
        _session.SignIn(UserId,"cashier","Cashier",
            Array.Empty<string>(),
            Array.Empty<string>());

        if (withCashShift)
            _session.SetActiveShift(CashShiftId, 1);
    }
    private Sale CreateCompletedSale(decimal quantity = 2m,decimal unitPrice = 100m,decimal paidAmount = 200m)
    {
        var sale = new Sale();
        var item = new SaleItem(ProductId,unitPrice,quantity);
        SetPrivateProperty(item, nameof(SaleItem.Id), SaleItemId);
        sale.AddItem(item);
        sale.AddPayment(new Payment(paidAmount, PaymentMethod.Card));
        sale.Complete();
        _sales.AddSale(SaleId, sale);
        return sale;
    }
    private static SaleReturnItemRequest CreateRequest(decimal quantity = 1m,bool isRestockable = true,int saleItemId = SaleItemId)
    {
        return new SaleReturnItemRequest(saleItemId,quantity,isRestockable);
    }
    private Task<int> ExecuteAsync(IReadOnlyCollection<SaleReturnItemRequest> items,PaymentMethod method = PaymentMethod.Card,string reason = "Customer return")
    {
        return CreateService().ExecuteAsync(SaleId,reason,items,method);
    }

    private static void SetPrivateProperty<T>(T instance,string propertyName,object value)
    {
        var property = typeof(T).GetProperty(propertyName,BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }

    // 1. Valid return completes successfully.
    [Fact]
    public async Task ProcessReturn_WithValidData_ShouldCompleteReturn()
    {
        SignIn();
        CreateCompletedSale();
        var returnId = await ExecuteAsync(new[] { CreateRequest() });
        Assert.True(returnId > 0);
        Assert.Single(_returns.Returns);
        Assert.Equal(SaleReturnStatus.Completed,_returns.Returns[0].Status);
        Assert.Equal(100m, _returns.Payments.Single().Amount);
    }

    // 2. User must be signed in.
    [Fact]
    public async Task ProcessReturn_WithoutSignedInUser_ShouldThrow()
    {
        CreateCompletedSale();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest() }));
        Assert.Empty(_returns.Returns);
    }

    // 3. Sale must exist.
    [Fact]
    public async Task ProcessReturn_WhenSaleDoesNotExist_ShouldThrow()
    {
        SignIn();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest() }));
        Assert.Empty(_returns.Returns);
    }

    // 4. Empty return requests are rejected.
    [Fact]
    public async Task ProcessReturn_WithNoItems_ShouldThrow()
    {
        SignIn();
        CreateCompletedSale();
        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteAsync(Array.Empty<SaleReturnItemRequest>()));
        Assert.Empty(_returns.Returns);
    }

    // 5. Duplicate sale items are rejected.
    [Fact]
    public async Task ProcessReturn_WithDuplicateSaleItem_ShouldThrow()
    {
        SignIn();
        CreateCompletedSale();

        var requests = new[]
        {
            CreateRequest(0.5m),
            CreateRequest(0.5m)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(requests));
        Assert.Empty(_returns.Returns);
    }

    // 6. Return quantity cannot exceed the quantity originally sold.
    [Fact]
    public async Task ProcessReturn_WithQuantityGreaterThanSold_ShouldThrow()
    {
        SignIn();
        CreateCompletedSale(quantity: 2m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest(quantity: 3m) }));

        Assert.Empty(_returns.Returns);
    }

    // 7. Previously returned quantities reduce the remaining quantity.
    [Fact]
    public async Task ProcessReturn_WithPreviouslyReturnedQuantity_ShouldValidateRemainingQuantity()
    {
        SignIn();
        CreateCompletedSale(quantity: 2m);
        _returns.PreviouslyReturnedQuantity = 1.5m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest(quantity: 0.6m) }));

        Assert.Empty(_returns.Returns);
    }

    // 8. Restockable items increase inventory.
    [Fact]
    public async Task ProcessReturn_WithRestockableItem_ShouldIncreaseStock()
    {
        SignIn();
        CreateCompletedSale();
        _stock.SetBalance(ProductId, 5m);
        await ExecuteAsync(new[] { CreateRequest(quantity: 1m, isRestockable: true) });
        Assert.Equal(6m, _stock.Balances[ProductId].Quantity);
        Assert.Single(_stock.Movements);
        Assert.Equal(StockMovementType.CustomerReturn,_stock.Movements[0].Type);
    }

    // 9. Non-restockable items do not increase inventory.
    [Fact]
    public async Task ProcessReturn_WithNonRestockableItem_ShouldNotIncreaseStock()
    {
        SignIn();
        CreateCompletedSale();
        _stock.SetBalance(ProductId, 5m);
        await ExecuteAsync(new[] { CreateRequest(quantity: 1m, isRestockable: false) });
        Assert.Equal(5m, _stock.Balances[ProductId].Quantity);
        Assert.Empty(_stock.Movements);
    }

    // 10. Refund cannot exceed the amount paid.
    [Fact]
    public async Task ProcessReturn_WithRefundGreaterThanPaidAmount_ShouldThrow()
    {
        SignIn();
        // Create a fully paid sale first.
        CreateCompletedSale(quantity: 2m,unitPrice: 100m,paidAmount: 200m);
        // Simulate a previous refund of 150.
        // The next refund is 100, so total refunds would be 250,
        // which exceeds the 200 originally paid.
        _returns.PreviouslyRefundedAmount = 150m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest(quantity: 1m) }));
        Assert.Empty(_returns.Returns);
    }

    // 11. Cash refunds require an active shift.
    [Fact]
    public async Task ProcessReturn_WithCashRefundWithoutActiveShift_ShouldThrow()
    {
        SignIn();
        CreateCompletedSale();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest() },PaymentMethod.Cash));
        Assert.Empty(_returns.Returns);
        Assert.Empty(_returns.CashMovements);
    }

    // 12. Cash refund records a cash-out movement.
    [Fact]
    public async Task ProcessReturn_WithCashRefund_ShouldRecordCashOutMovement()
    {
        SignIn(withCashShift: true);
        CreateCompletedSale();
        _returns.SetCashShift(CashShiftId,new CashShift(UserId, 1, 100m));
        await ExecuteAsync(new[] { CreateRequest() },PaymentMethod.Cash);
        var movement = Assert.Single(_returns.CashMovements);
        Assert.Equal(CashShiftId, movement.CashShiftId);
        Assert.Equal(100m, movement.Amount);
        Assert.False(movement.IncreasesDrawer);
        Assert.Equal(CashMovementType.CashRefund, movement.Type);
    }

    // 13. Previously refunded amounts are included in the refund limit.
    [Fact]
    public async Task ProcessReturn_WhenPreviousRefundsExhaustPaidAmount_ShouldThrow()
    {
        SignIn();
        CreateCompletedSale(quantity: 2m,unitPrice: 100m,paidAmount: 200m);
        _returns.PreviouslyRefundedAmount = 150m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest(quantity: 1m) }));
        Assert.Empty(_returns.Returns);
    }

    // 14. Cash refunds reject a shift that is not open.
    [Fact]
    public async Task ProcessReturn_WithClosedCashShift_ShouldThrow()
    {
        SignIn(withCashShift: true);
        CreateCompletedSale();
        var shift = new CashShift(UserId, 1, 100m);
        SetPrivateProperty(shift,nameof(CashShift.Status),CashShiftStatus.Closed);
        _returns.SetCashShift(CashShiftId, shift);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(new[] { CreateRequest() },PaymentMethod.Cash));
        Assert.Empty(_returns.Returns);
    }

    // 15. Return quantity must use at most three decimal places.
    [Fact]
    public async Task ProcessReturn_WithTooManyQuantityDecimals_ShouldThrow()
    {
        SignIn();
        CreateCompletedSale(quantity: 2m);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ExecuteAsync(new[] { CreateRequest(quantity: 0.1234m) }));
        Assert.Empty(_returns.Returns);
    }
}

// Test-only repositories and transaction implementation.
// These names are intentionally unique to this test file.

public sealed class ReturnTestSaleRepository : ISaleRepository
{
    private readonly Dictionary<int, Sale> _sales = new();
    public void AddSale(int id, Sale sale) => _sales[id] = sale;
    public Task<Sale?> GetByIdAsync(int id)
    {
        _sales.TryGetValue(id, out var sale);
        return Task.FromResult(sale);
    }
    public Task AddAsync(Sale sale) => throw new NotSupportedException();
    public Task UpdateAsync(Sale sale) => throw new NotSupportedException();
    public Task RegisterSplitPaymentAsync(Sale sale) => throw new NotSupportedException();
}

public sealed class ReturnTestRepository : ISaleReturnRepository
{
    private int _nextId = 1;
    private readonly Dictionary<int, CashShift> _shifts = new();
    public List<SaleReturn> Returns { get; } = new();
    public List<SaleReturnPayment> Payments { get; } = new();
    public List<CashMovement> CashMovements { get; } = new();
    public decimal PreviouslyReturnedQuantity { get; set; }
    public decimal PreviouslyRefundedAmount { get; set; }
    public void SetCashShift(int id, CashShift shift) =>  _shifts[id] = shift;
    public Task<decimal> GetPreviouslyReturnedQuantityAsync(int saleItemId) => Task.FromResult(PreviouslyReturnedQuantity);
    public Task<decimal> GetPreviouslyRefundedAmountAsync(int saleId) => Task.FromResult(PreviouslyRefundedAmount);
    public Task<CashShift?> GetCashShiftAsync(int cashShiftId)
    {
        _shifts.TryGetValue(cashShiftId, out var shift);
        return Task.FromResult(shift);
    }

    public Task AddAsync(SaleReturn saleReturn)
    {
        SetPrivateId(saleReturn, _nextId++);
        Returns.Add(saleReturn);
        return Task.CompletedTask;
    }

    public Task AddPaymentAsync(SaleReturnPayment payment)
    {
        Payments.Add(payment);
        return Task.CompletedTask;
    }

    public Task AddCashMovementAsync(CashMovement movement)
    {
        CashMovements.Add(movement);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;

    private static void SetPrivateId(SaleReturn saleReturn, int id)
    {
        var property = typeof(SaleReturn).GetProperty(nameof(SaleReturn.Id),BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(saleReturn, id);
    }
}

public sealed class ReturnTestStockRepository : IStockRepository
{
    public Dictionary<int, StockBalance> Balances { get; } = new();
    public List<StockMovement> Movements { get; } = new();

    public void SetBalance(int productId, decimal quantity)
    {
        Balances[productId] = new StockBalance(productId, quantity);
    }

    public Task<StockBalance?> GetBalanceAsync(int productId)
    {
        Balances.TryGetValue(productId, out var balance);
        return Task.FromResult(balance);
    }

    public Task AddBalanceAsync(StockBalance balance)
    {
        Balances[balance.ProductId] = balance;
        return Task.CompletedTask;
    }

    public Task UpdateBalanceAsync(StockBalance balance)
    {
        Balances[balance.ProductId] = balance;
        return Task.CompletedTask;
    }

    public Task AddMovementAsync(StockMovement movement)
    {
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;

    public Task<bool> ProductExistsAsync(int productId) => Task.FromResult(true);
}

public sealed class ReturnTestUnitOfWork : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        await operation();
    }
}