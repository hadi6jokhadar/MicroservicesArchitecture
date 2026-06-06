using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using IhsanDev.Shared.Kernel.Entities;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Application.Services;

namespace IhsanDev.Shared.Infrastructure.Persistence;

public abstract class BaseDbContext : DbContext
{
    // Timestamp and tracking fields are already captured in the action name and
    // top-level audit fields — excluding them keeps Before/After payloads lean.
    private static readonly FrozenSet<string> SnapshotExcluded = new HashSet<string>
    {
        nameof(BaseEntity.Created),
        nameof(BaseEntity.LastModified),
        nameof(BaseEntity.CreatedBy),
        nameof(BaseEntity.LastModifiedBy),
    }.ToFrozenSet();

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

        // 2. Capture entity changes into the audit pending list (no JSON yet)
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

        // 3. Capture connection string before the save (available once context is configured)
        var connectionString = Database.GetConnectionString() ?? string.Empty;

        // 4. Save business data only — audit rows go through the background channel
        var result = await base.SaveChangesAsync(cancellationToken);

        // 5. Publish audit entries to channel after successful save (non-blocking)
        _auditService?.Commit(connectionString);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLogEntity>(e =>
        {
            // Composite indexes that match the query handler's filter + sort patterns
            e.HasIndex(x => new { x.TenantId, x.OccurredAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_AuditLogs_TenantId_OccurredAt");

            e.HasIndex(x => new { x.EntityType, x.OccurredAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_AuditLogs_EntityType_OccurredAt");

            e.HasIndex(x => new { x.UserId, x.OccurredAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_AuditLogs_UserId_OccurredAt");
        });
    }

    // Returns scalar properties only, excluding internal tracking fields to keep payloads lean.
    private static Dictionary<string, object?> Snapshot(PropertyValues values)
        => values.Properties
            .Where(p => !SnapshotExcluded.Contains(p.Name))
            .ToDictionary(p => p.Name, p => values[p]);
}
