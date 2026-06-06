using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace Notification.Infrastructure.Persistence;

/// <summary>
/// Global database context for notification queue management
/// This database is NOT tenant-specific - it's shared across all tenants
/// </summary>
public class NotificationDbContext : BaseDbContext
{
    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null)
        : base(options, currentUserService, auditService)
    {
    }

    public DbSet<NotificationQueueItem> NotificationQueue { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationQueueItem>(entity =>
        {
            entity.ToTable("NotificationQueue");
            
            // Composite index for queue processing (fetch pending items)
            entity.HasIndex(e => new { e.QueueStatus, e.ExpiresAt, e.NextRetryAt, e.Priority, e.Created })
                .HasDatabaseName("IX_NotificationQueue_Processing")
                .HasFilter("\"QueueStatus\" = 0"); // Only pending items
            
            // Composite index for cleanup operations (critical for 100k+ scale)
            entity.HasIndex(e => new { e.QueueStatus, e.LastModified })
                .HasDatabaseName("IX_NotificationQueue_Cleanup")
                .HasFilter("\"QueueStatus\" IN (2, 3, 4)"); // Sent, Failed, Expired
            
            // Index for expiration checks (filter removed - NOW() is not immutable in PostgreSQL)
            entity.HasIndex(e => new { e.ExpiresAt, e.QueueStatus })
                .HasDatabaseName("IX_NotificationQueue_Expiration")
                .HasFilter("\"QueueStatus\" = 0");
            
            // Tenant-based queries
            entity.HasIndex(e => new { e.TenantId, e.QueueStatus, e.Created })
                .HasDatabaseName("IX_NotificationQueue_Tenant");
            
            // User-based queries
            entity.HasIndex(e => new { e.UserId, e.QueueStatus, e.Created })
                .HasDatabaseName("IX_NotificationQueue_User");

            entity.Property(e => e.TenantId)
                .HasMaxLength(100);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Message)
                .HasMaxLength(2000);

            entity.Property(e => e.Data)
                .HasColumnType("jsonb");

            entity.Property(e => e.Error)
                .HasMaxLength(2000);

            entity.Property(e => e.DeliveryType)
                .HasConversion<int>();

            entity.Property(e => e.Priority)
                .HasConversion<int>();

            entity.Property(e => e.QueueStatus)
                .HasConversion<int>();
        });
    }
}
