
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Abstractions;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync();

        try
        {
            await operation();

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
