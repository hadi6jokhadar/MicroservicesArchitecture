using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
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
        ICurrentUserService? currentUserService = null)
        : base(options, currentUserService)
    {
    }

    public DbSet<NotificationQueueItem> NotificationQueue { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationQueueItem>(entity =>
        {
            entity.ToTable("NotificationQueue");
            
            entity.HasIndex(e => new { e.QueueStatus, e.Created })
                .HasDatabaseName("IX_NotificationQueue_Status_Created");
            
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_NotificationQueue_TenantId");
            
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_NotificationQueue_UserId");
            
            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_NotificationQueue_ExpiresAt");

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
