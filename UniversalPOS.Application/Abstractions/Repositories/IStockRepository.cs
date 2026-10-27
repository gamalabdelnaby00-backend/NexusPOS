
using UniversalPOS.Domain.Entities;

namespace UniversalPOS.Application.Abstractions.Repositories;

public interface IStockRepository
{
    Task<StockBalance?> GetBalanceAsync(int productId);

    Task AddBalanceAsync(StockBalance balance);

    Task UpdateBalanceAsync(StockBalance balance);

    Task AddMovementAsync(StockMovement movement);

    Task SaveChangesAsync();

    Task<bool> ProductExistsAsync(int productId);
}
