using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public class StockRepository : IStockRepository
{
    private readonly AppDbContext _dbContext;

    public StockRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StockBalance?> GetBalanceAsync(int productId)
    {
        return await _dbContext.StockBalances.FirstOrDefaultAsync(x => x.ProductId == productId);
    }

    public async Task AddBalanceAsync(StockBalance balance)
    {
        await _dbContext.StockBalances.AddAsync(balance);
    }

    public async Task AddMovementAsync(StockMovement movement)
    {
        await _dbContext.StockMovements.AddAsync(movement);
    }

    public async Task SaveChangesAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Stock was changed by another operation. " + "Reload the stock balance before trying again.");
        }
    }
    public async Task<bool> ProductExistsAsync(int productId)
    {
        return await _dbContext.Products.AnyAsync(p => p.Id == productId);
    }
    public Task UpdateBalanceAsync(StockBalance balance)
    {
        _dbContext.StockBalances.Update(balance);
        return Task.CompletedTask;
    }

}
