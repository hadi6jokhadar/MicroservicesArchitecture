using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using IhsanDev.Shared.Kernel.Entities;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Application.Services;

namespace IhsanDev.Shared.Infrastructure.Persistence;

public abstract class BaseDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private readonly IAuditService? _auditService;

    protected BaseDbContext(
        DbContextOptions options,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Stamp timestamps first so 'after' snapshots include them
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

        // 2. Auto-capture every entity change before flushing to DB
        if (_auditService != null)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                var entityType = entry.Entity.GetType().Name;
                var entityId = entry.Entity.Id > 0 ? entry.Entity.Id.ToString() : null;

                string action;
                object? before = null;
                object? after = null;

                switch (entry.State)
                {
                    case EntityState.Added:
                        action = $"{entityType}.Created";
                        after = Snapshot(entry.CurrentValues);
                        break;

                    case EntityState.Deleted:
                        action = $"{entityType}.HardDeleted";
                        before = Snapshot(entry.OriginalValues);
                        break;

                    default: // Modified — detect soft-delete via IsArchived flag
                        var wasArchived = entry.OriginalValues[nameof(BaseEntity.IsArchived)] is true;
                        var nowArchived = entry.CurrentValues[nameof(BaseEntity.IsArchived)] is true;
                        action = (!wasArchived && nowArchived) ? $"{entityType}.Deleted" : $"{entityType}.Updated";
                        before = Snapshot(entry.OriginalValues);
                        after = Snapshot(entry.CurrentValues);
                        break;
                }

                _auditService.Record(action, entityType, entityId, before, after);
            }
        }

        // 3. Flush pending rows (auto-captured above) atomically with the main save
        var auditRows = _auditService?.Flush();
        if (auditRows?.Count > 0)
            AuditLogs.AddRange(auditRows);

        return await base.SaveChangesAsync(cancellationToken);
    }

    // Returns a plain dictionary of scalar property name → value (no navigation properties)
    private static Dictionary<string, object?> Snapshot(PropertyValues values)
        => values.Properties.ToDictionary(p => p.Name, p => values[p]);
}
