using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

namespace Notification.Infrastructure.Persistence;

/// <summary>
/// Tenant-specific database context for notification history
/// Each tenant has their own notifications table in their own database
/// </summary>
public class TenantNotificationDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<TenantNotificationDbContext>? _logger;

    public TenantNotificationDbContext(
        DbContextOptions<TenantNotificationDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<TenantNotificationDbContext>? logger = null)
        : base(options, currentUserService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<Domain.Entities.Notification> Notifications { get; set; }

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
                    npgsqlOptions.MigrationsAssembly(typeof(TenantNotificationDbContext).Assembly.GetName().Name);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;

            case "Sqlite":
                optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(TenantNotificationDbContext).Assembly.GetName().Name);
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

        modelBuilder.Entity<Domain.Entities.Notification>(entity =>
        {
            entity.ToTable("Notifications");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Notifications_UserId");

            entity.HasIndex(e => new { e.UserId, e.IsRead })
                .HasDatabaseName("IX_Notifications_UserId_IsRead");

            entity.HasIndex(e => e.Created)
                .HasDatabaseName("IX_Notifications_Created");

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Message)
                .HasMaxLength(2000);

            entity.Property(e => e.Data)
                .HasColumnType("jsonb");
        });
    }
}
