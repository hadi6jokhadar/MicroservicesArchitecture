using Backup.Application.Interfaces;
using Backup.Domain.Enums;
using Backup.Infrastructure.Options;
using Microsoft.Extensions.Configuration;

namespace Backup.Infrastructure.Jobs;

/// <summary>
/// Resolves the connection string for a given backup scope/service/tenant. Shared by
/// <see cref="RunBackupJob"/> and <see cref="RunRestoreJob"/> so restore's "re-resolve exactly
/// like the backup job did" requirement doesn't duplicate the lookup logic.
/// </summary>
public class BackupConnectionResolver
{
    private readonly IConfiguration _configuration;
    private readonly ITenantDirectoryClient _tenantDirectoryClient;

    public BackupConnectionResolver(IConfiguration configuration, ITenantDirectoryClient tenantDirectoryClient)
    {
        _configuration = configuration;
        _tenantDirectoryClient = tenantDirectoryClient;
    }

    public async Task<string?> ResolveAsync(BackupScope scope, string? serviceName, string? tenantId, CancellationToken ct)
    {
        if (scope == BackupScope.GlobalService)
        {
            var globalTargets = _configuration.GetSection("Backup:GlobalTargets").Get<List<GlobalTargetOptions>>() ?? [];
            return globalTargets
                .FirstOrDefault(t => string.Equals(t.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                ?.ConnectionString;
        }

        var tenants = await _tenantDirectoryClient.GetActiveTenantsAsync(ct);
        return tenants
            .FirstOrDefault(t => string.Equals(t.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            ?.ConnectionString;
    }
}
