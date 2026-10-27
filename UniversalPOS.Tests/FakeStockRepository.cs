
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;

public sealed class FakeStockRepository : IStockRepository
{
    private readonly Dictionary<int, StockBalance> _balances = new();
    private readonly HashSet<int> _productIds = new();
    private readonly List<StockMovement> _movements = new();

    public IReadOnlyList<StockMovement> Movements => _movements;
    public int UpdateBalanceCallCount { get; private set; }

    public void AddProduct(int productId)
    {
        _productIds.Add(productId);
    }

    public Task<StockBalance?> GetBalanceAsync(int productId)
    {
        _balances.TryGetValue(productId, out var balance);
        return Task.FromResult(balance);
    }

    public Task AddBalanceAsync(StockBalance balance)
    {
        _balances.Add(balance.ProductId, balance);
        return Task.CompletedTask;
    }

    public Task UpdateBalanceAsync(StockBalance balance)
    {
        _balances[balance.ProductId] = balance;
        UpdateBalanceCallCount++;
        return Task.CompletedTask;
    }

    public Task AddMovementAsync(StockMovement movement)
    {
        _movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return Task.CompletedTask;
    }

    public Task<bool> ProductExistsAsync(int productId)
    {
        return Task.FromResult(_productIds.Contains(productId));
    }
}
