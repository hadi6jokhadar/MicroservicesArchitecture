using FileManager.Domain.Entities;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileManager.Infrastructure.Persistence;

public class FileManagerDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<FileManagerDbContext>? _logger;

    public FileManagerDbContext(
        DbContextOptions<FileManagerDbContext> options,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<FileManagerDbContext>? logger = null)
        : base(options)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<FileManagerEntity> FileManager => Set<FileManagerEntity>();

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
                    npgsqlOptions.MigrationsAssembly(typeof(FileManagerDbContext).Assembly.GetName().Name);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;

            case "Sqlite":
                optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(FileManagerDbContext).Assembly.GetName().Name);
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FileManagerDbContext).Assembly);
    }
}
