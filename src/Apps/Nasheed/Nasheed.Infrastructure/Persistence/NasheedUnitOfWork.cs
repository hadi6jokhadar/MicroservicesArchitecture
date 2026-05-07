using Microsoft.EntityFrameworkCore;
using Nasheed.Application.Interfaces;

namespace Nasheed.Infrastructure.Persistence;

public class NasheedUnitOfWork : INasheedUnitOfWork
{
    private readonly NasheedDbContext _dbContext;

    public NasheedUnitOfWork(NasheedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await operation(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
    }
}
