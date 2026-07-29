using Backup.Domain.Entities;
using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Persistence;

/// <summary>
/// Global database context for the Backup service (Database Strategy A — Single Global DB).
/// Backup is NOT multi-tenant: it stores its own operational metadata (backup targets, backup run
/// history, restore run history) in one shared database, regardless of which service/tenant a
/// given backup target refers to. No <c>ITenantContext</c> is used here — see
/// <c>.claude/instructions/database-strategy.instructions.md</c> Strategy A.
/// </summary>
public class BackupDbContext : BaseDbContext
{
    public BackupDbContext(
        DbContextOptions<BackupDbContext> options,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null)
        : base(options, currentUserService, auditService)
    {
    }

    public DbSet<BackupTargetEntity> BackupTargets => Set<BackupTargetEntity>();

    public DbSet<BackupRunEntity> BackupRuns => Set<BackupRunEntity>();

    public DbSet<RestoreRunEntity> RestoreRuns => Set<RestoreRunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BackupTargetEntity>(entity =>
        {
            entity.Property(e => e.Scope).HasConversion<int>();

            entity.Property(e => e.ServiceName)
                .HasMaxLength(100);

            entity.Property(e => e.TenantId)
                .HasMaxLength(100);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true);

            entity.HasIndex(e => e.ServiceName)
                .HasFilter("\"IsArchived\" = false");

            entity.HasIndex(e => e.TenantId)
                .HasFilter("\"IsArchived\" = false");
        });

        modelBuilder.Entity<BackupRunEntity>(entity =>
        {
            entity.Property(e => e.Scope).HasConversion<int>();
            entity.Property(e => e.TriggerType).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.LocalStatus).HasConversion<int>();
            entity.Property(e => e.CloudStatus).HasConversion<int>();

            entity.Property(e => e.ServiceName)
                .HasMaxLength(100);

            entity.Property(e => e.TenantId)
                .HasMaxLength(100);

            entity.Property(e => e.DatabaseName)
                .HasMaxLength(100);

            entity.Property(e => e.TriggeredByEmail)
                .HasMaxLength(256);

            entity.Property(e => e.LocalFilePath)
                .HasMaxLength(1000);

            entity.Property(e => e.CloudStorageKey)
                .HasMaxLength(500);

            entity.Property(e => e.Sha256Checksum)
                .HasMaxLength(64);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            // Plain FK column — no navigation property. Deleting a target keeps this run's
            // history intact (denormalized Scope/ServiceName/TenantId snapshot survives).
            entity.HasOne<BackupTargetEntity>()
                .WithMany()
                .HasForeignKey(e => e.BackupTargetId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.Scope, e.ServiceName, e.TenantId, e.Created })
                .HasDatabaseName("IX_BackupRuns_Scope_Service_Tenant_Created");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_BackupRuns_Status");
        });

        modelBuilder.Entity<RestoreRunEntity>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<int>();

            entity.Property(e => e.TargetConnectionOverride)
                .HasMaxLength(1000);

            entity.Property(e => e.TriggeredByEmail)
                .HasMaxLength(256);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            // Required FK — restore history must always point at a real backup run.
            // Restrict: a backup run cannot be deleted while restore history references it.
            entity.HasOne<BackupRunEntity>()
                .WithMany()
                .HasForeignKey(e => e.BackupRunId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.HasIndex(e => e.BackupRunId)
                .HasDatabaseName("IX_RestoreRuns_BackupRunId");
        });
    }
}
