using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly AppDbContext _dbContext;

    public SaleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Sale?> GetByIdAsync(int id)
    {
        return await _dbContext.Sales.Include(s => s.Items).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AddAsync(Sale sale)
    {
        await _dbContext.Sales.AddAsync(sale);
        await _dbContext.SaveChangesAsync();
    }


    public async Task UpdateAsync(Sale sale)
    {
        await _dbContext.SaveChangesAsync();
    }
    public async Task RegisterSplitPaymentAsync(Sale sale)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
