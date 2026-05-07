namespace Nasheed.Application.Interfaces;

public interface INasheedUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
