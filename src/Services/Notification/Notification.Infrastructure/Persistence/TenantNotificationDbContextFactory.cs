using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using IhsanDev.Shared.Kernel.Interfaces.Tenant;
using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace Notification.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating TenantNotificationDbContext during migrations.
/// This bypasses multi-tenancy requirements by using appsettings.json directly.
/// </summary>
public class TenantNotificationDbContextFactory : IDesignTimeDbContextFactory<TenantNotificationDbContext>
{
    public TenantNotificationDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Notification.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration["DatabaseSettings:ConnectionString"] 
            ?? throw new InvalidOperationException("Database connection string not found in appsettings.json");
        
        // For tenant DB, use a different database name
        connectionString = connectionString.Replace("notifications_global", "notifications_tenant_default");
        
        var provider = configuration["DatabaseSettings:Provider"] ?? "PostgreSql";

        var optionsBuilder = new DbContextOptionsBuilder<TenantNotificationDbContext>();

        switch (provider)
        {
            case "PostgreSql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(TenantNotificationDbContext).Assembly.GetName().Name);
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

        // Create context with a mock tenant context (for migrations only)
        var mockTenantContext = new MockTenantContext();
        return new TenantNotificationDbContext(optionsBuilder.Options, null, mockTenantContext, configuration);
    }

    /// <summary>
    /// Mock tenant context for design-time migrations
    /// </summary>
    private class MockTenantContext : ITenantContext
    {
        public TenantInfo? CurrentTenant => null;
        public string? TenantId => "default";
        public bool IsMultiTenantMode => false;
        public bool HasTenant => false;
        public void SetTenant(TenantInfo? tenantInfo) { }
    }
}
