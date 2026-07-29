using Backup.Application.Interfaces;
using Backup.Domain.Entities;
using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Jobs;

/// <summary>
/// Keeps <see cref="BackupTargetEntity"/> rows in sync with the Tenant Service's active tenant
/// list. Only ever adds new targets for tenants that have a dedicated database connection string
/// and don't already have a target — never disables an existing target automatically, since an
/// admin's manual disable must stick.
/// </summary>
public class TenantTargetSyncJob
{
    private readonly BackupDbContext _context;
    private readonly ITenantDirectoryClient _tenantDirectoryClient;
    private readonly ILogger<TenantTargetSyncJob> _logger;

    public TenantTargetSyncJob(
        BackupDbContext context,
        ITenantDirectoryClient tenantDirectoryClient,
        ILogger<TenantTargetSyncJob> logger)
    {
        _context = context;
        _tenantDirectoryClient = tenantDirectoryClient;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken ct)
    {
        var tenants = await _tenantDirectoryClient.GetActiveTenantsAsync(ct);

        var existingTenantIds = await _context.BackupTargets
            .Where(t => t.Scope == BackupScope.Tenant && t.TenantId != null)
            .Select(t => t.TenantId!)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingTenantIds, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive || string.IsNullOrWhiteSpace(tenant.ConnectionString))
            {
                continue;
            }

            if (existingSet.Contains(tenant.TenantId))
            {
                continue;
            }

            _context.BackupTargets.Add(new BackupTargetEntity
            {
                Scope = BackupScope.Tenant,
                TenantId = tenant.TenantId,
                DisplayName = tenant.TenantName,
                IsEnabled = true
            });
            existingSet.Add(tenant.TenantId);
            added++;
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("TenantTargetSyncJob: added {Count} new tenant backup target(s)", added);
        }
    }
}
