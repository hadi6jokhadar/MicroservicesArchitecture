using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        ILogger<IdentityDbContext>? logger = null) 
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

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

        // When multi-tenancy is enabled, ONLY use tenant-specific database settings
        if (multiTenancyEnabled)
        {
            // Tenant context and configuration MUST be present
            if (_tenantContext?.HasTenant != true || 
                _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
            {
                throw new InvalidOperationException(
                    "Multi-tenancy is enabled but tenant database configuration is not available. " +
                    "Ensure x-tenant-id header is provided and tenant exists with valid database settings.");
            }

            var tenantDb = _tenantContext.CurrentTenant.Configuration.DatabaseSettings;
            
            if (string.IsNullOrWhiteSpace(tenantDb.ConnectionString))
            {
                throw new InvalidOperationException(
                    $"Tenant '{_tenantContext.CurrentTenant.TenantId}' does not have a database connection string configured.");
            }

            connectionString = tenantDb.ConnectionString;
            provider = tenantDb.Provider ?? "PostgreSql";
            
            _logger?.LogInformation(
                "Using tenant-specific database connection for tenant: {TenantId}", 
                _tenantContext.CurrentTenant.TenantId);
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
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Role).HasConversion<string>();
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
    }
}