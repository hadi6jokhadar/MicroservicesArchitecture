using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Entities;

namespace Identity.Infrastructure.Persistence;

public class IdentityDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<IdentityDbContext>? _logger;

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<IdentityDbContext>? logger = null,
        IAuditService? auditService = null)
        : base(options, currentUserService, auditService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RoleClaim> RoleClaims => Set<RoleClaim>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // If already configured (from DI), skip
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        string? connectionString = null;
        string? provider = null;
        var multiTenancyEnabled = _configuration?.GetValue<bool>("MultiTenancy:Enabled", false) ?? false;

        if (multiTenancyEnabled)
        {
            // When multi-tenancy is enabled, x-tenant-id is now optional
            // Fall back to global database if no tenant context or tenant has no database config
            if (_tenantContext?.HasTenant != true || 
                _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
            {
                // Use global database from appsettings.json as fallback
                _logger?.LogDebug("No tenant context or tenant database config - using global database from appsettings.json");
                
                if (_configuration == null)
                {
                    throw new InvalidOperationException("Configuration is not available");
                }

                connectionString = _configuration["DatabaseSettings:ConnectionString"];
                provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
                
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "Database connection string is not configured in appsettings.json");
                }
            }
            else
            {
                // Use tenant-specific database
                var tenantDb = _tenantContext.CurrentTenant.Configuration.DatabaseSettings;
                
                if (string.IsNullOrWhiteSpace(tenantDb.ConnectionString))
                {
                    throw new InvalidOperationException(
                        $"Tenant '{_tenantContext.CurrentTenant.TenantId}' does not have a database connection string configured.");
                }

                provider = tenantDb.Provider ?? "PostgreSql";

                var maxPoolSizePerTenant = _configuration?.GetValue("DatabaseSettings:MaxPoolSizePerTenant", 20) ?? 20;
                connectionString = provider == "PostgreSql"
                    ? NpgsqlConnectionStringHelper.WithBoundedPoolSize(tenantDb.ConnectionString, maxPoolSizePerTenant)
                    : tenantDb.ConnectionString;

                _logger?.LogInformation(
                    "Using tenant-specific database connection for tenant: {TenantId}",
                    _tenantContext.CurrentTenant.TenantId);
            }
        }
        else
        {
            // When multi-tenancy is disabled, use appsettings.json
            if (_configuration == null)
            {
                throw new InvalidOperationException("Configuration is not available");
            }

            connectionString = _configuration["DatabaseSettings:ConnectionString"];
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string is not configured in appsettings.json");
            }

            _logger?.LogDebug("Using default database connection from appsettings.json");
        }

        // Configure database provider
        switch (provider)
        {
            case "PostgreSql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;

            case "Sqlite":
                optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.GetName().Name);
                });
                break;

            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported");
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasFilter("\"IsArchived\" = false");
                
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            
            // Many-to-many relationship with Roles
            entity.HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Token);
            entity.HasIndex(e => new { e.UserId, e.Platform });
            entity.Property(e => e.Token).HasMaxLength(500).IsRequired();
            entity.Property(e => e.DeviceIdentifier).HasMaxLength(100);
            entity.Property(e => e.Platform).HasConversion<string>();
        });

        // Role configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName)
                .IsUnique()
                .HasFilter("\"IsArchived\" = false");
                
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            
            entity.HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasMany(r => r.RoleClaims)
                .WithOne(rc => rc.Role)
                .HasForeignKey(rc => rc.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Claim configuration
        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasIndex(e => e.ClaimValue)
                .IsUnique()
                .HasFilter("\"IsArchived\" = false");
                
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ClaimType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ClaimValue).HasMaxLength(200).IsRequired();
            
            entity.HasMany(c => c.RoleClaims)
                .WithOne(rc => rc.Claim)
                .HasForeignKey(rc => rc.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserRole junction table configuration
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.RoleId })
                .IsUnique()
                .HasFilter("\"IsArchived\" = false");
        });

        // RoleClaim junction table configuration
        modelBuilder.Entity<RoleClaim>(entity =>
        {
            entity.HasIndex(e => new { e.RoleId, e.ClaimId })
                .IsUnique()
                .HasFilter("\"IsArchived\" = false");
        });
    }
}