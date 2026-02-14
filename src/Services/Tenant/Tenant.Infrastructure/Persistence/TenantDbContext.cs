using Tenant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;

namespace Tenant.Infrastructure.Persistence;

public class TenantDbContext : BaseDbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options, ICurrentUserService? currentUserService = null) 
        : base(options, currentUserService)
    {
    }

    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantSettings>(entity =>
        {
            // Unique indexes
            entity.HasIndex(e => e.TenantId)
                .IsUnique()
                .HasFilter("\"IsArchived\" = false");
                
            entity.HasIndex(e => e.UserId)
                .IsUnique() // Each user can have exactly one tenant
                .HasFilter("\"IsArchived\" = false");
            
            // Property configurations
            entity.Property(e => e.TenantId)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.TenantName)
                .HasMaxLength(200)
                .IsRequired();
            
            entity.Property(e => e.Data)
                .IsRequired();
            
            entity.Property(e => e.StartDate)
                .IsRequired();
            
            entity.Property(e => e.ExpireDate)
                .IsRequired();
            
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            // Computed column for validation
            entity.Ignore(e => e.IsExpired);
            entity.Ignore(e => e.IsValid);
        });
    }
}
