
using Microsoft.EntityFrameworkCore;
using System.Data;
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
            // Serializable isolation protects read-then-write workflows (such as sale returns)
            // from two concurrent requests both passing the remaining-quantity/refund checks.
            await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

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
