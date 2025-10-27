using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;

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

        // Check if multi-tenancy is enabled and tenant has custom database settings
        if (_tenantContext?.HasTenant == true && 
            _tenantContext.CurrentTenant?.Configuration?.Database != null)
        {
            var tenantDb = _tenantContext.CurrentTenant.Configuration.Database;
            
            if (!string.IsNullOrWhiteSpace(tenantDb.ConnectionString))
            {
                connectionString = tenantDb.ConnectionString;
                provider = tenantDb.Provider ?? "PostgreSql";
                
                _logger?.LogInformation(
                    "Using tenant-specific database connection for tenant: {TenantId}", 
                    _tenantContext.CurrentTenant.TenantId);
            }
        }

        // Fallback to appsettings.json if no tenant-specific database
        if (string.IsNullOrWhiteSpace(connectionString) && _configuration != null)
        {
            connectionString = _configuration["DatabaseSettings:ConnectionString"];
            provider = _configuration["DatabaseSettings:Provider"] ?? "PostgreSql";
            
            _logger?.LogDebug("Using default database connection from appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured in either tenant settings or appsettings.json");
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
    }
}