
using UniversalPOS.Application.Abstractions;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int TransactionCallCount { get; private set; }

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        TransactionCallCount++;
        await operation();
    }
}
