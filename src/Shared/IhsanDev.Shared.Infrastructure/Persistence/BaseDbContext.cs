using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Kernel.Entities;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace IhsanDev.Shared.Infrastructure.Persistence;

public abstract class BaseDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    protected BaseDbContext(DbContextOptions options, ICurrentUserService? currentUserService = null) 
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Created = DateTime.UtcNow;
                    entry.Entity.CreatedBy = _currentUserService?.UserId;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModified = DateTime.UtcNow;
                    entry.Entity.LastModifiedBy = _currentUserService?.UserId;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}