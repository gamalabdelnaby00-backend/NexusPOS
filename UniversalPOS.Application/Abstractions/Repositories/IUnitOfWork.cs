namespace UniversalPOS.Application.Abstractions;

public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<Task> operation);
}
