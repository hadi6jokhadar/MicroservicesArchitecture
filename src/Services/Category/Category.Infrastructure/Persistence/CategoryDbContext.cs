using IhsanDev.Shared.Application.Services;
using IhsanDev.Shared.Infrastructure.Persistence;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Category.Domain.Entities;

namespace Category.Infrastructure.Persistence;

public class CategoryDbContext : BaseDbContext
{
    private readonly ITenantContext? _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<CategoryDbContext>? _logger;

    public CategoryDbContext(
        DbContextOptions<CategoryDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null,
        IConfiguration? configuration = null,
        ILogger<CategoryDbContext>? logger = null,
        IAuditService? auditService = null)
        : base(options, currentUserService, auditService)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    /// <summary>
    /// Transactional outbox — pending Redis Pub/Sub events waiting to be delivered.
    /// Written by <c>OutboxCategoryEventPublisher</c>; consumed by <c>OutboxEventProcessorService</c>.
    /// </summary>
    public DbSet<OutboxEventEntity> OutboxEvents => Set<OutboxEventEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
            return;
        }

        string? connectionString;
        string? provider;
        var multiTenancyEnabled = _configuration?.GetValue<bool>("MultiTenancy:Enabled", false) ?? false;

        if (multiTenancyEnabled)
        {
            if (_tenantContext?.HasTenant != true ||
                _tenantContext.CurrentTenant?.Configuration?.DatabaseSettings == null)
            {
                _logger?.LogDebug("No tenant context — using global fallback DB");
                connectionString = _configuration?["DatabaseSettings:ConnectionString"]
                    ?? throw new InvalidOperationException("DatabaseSettings:ConnectionString not configured");
                provider = _configuration?["DatabaseSettings:Provider"] ?? "PostgreSql";
            }
            else
            {
                var tenantDb = _tenantContext.CurrentTenant.Configuration.DatabaseSettings;
                var tenantConnectionString = tenantDb.ConnectionString
                    ?? throw new InvalidOperationException(
                        $"Tenant '{_tenantContext.TenantId}' has no database connection string configured");
                provider = tenantDb.Provider ?? "PostgreSql";

                var maxPoolSizePerTenant = _configuration?.GetValue("DatabaseSettings:MaxPoolSizePerTenant", 20) ?? 20;
                connectionString = provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
                    ? NpgsqlConnectionStringHelper.WithBoundedPoolSize(tenantConnectionString, maxPoolSizePerTenant)
                    : tenantConnectionString;

                _logger?.LogInformation("Using tenant DB for tenant '{TenantId}'", _tenantContext.TenantId);
            }
        }
        else
        {
            connectionString = _configuration?["DatabaseSettings:ConnectionString"]
                ?? throw new InvalidOperationException("DatabaseSettings:ConnectionString not configured");
            provider = _configuration?["DatabaseSettings:Provider"] ?? "PostgreSql";
        }

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
            optionsBuilder.UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly(typeof(CategoryDbContext).Assembly.GetName().Name);
                o.EnableRetryOnFailure(maxRetryCount: 3);
            });
        }
        else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString, o =>
                o.MigrationsAssembly(typeof(CategoryDbContext).Assembly.GetName().Name));
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CategoryDbContext).Assembly);
    }
}
