using Backup.Domain.Entities;
using Backup.Domain.Enums;
using Backup.Infrastructure.Options;
using Backup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Jobs;

/// <summary>
/// Keeps <see cref="BackupTargetEntity"/> rows in sync with the statically-configured
/// <c>Backup:GlobalTargets</c> section — one target per service that has its own global/fallback
/// database. Only ever adds targets for entries that don't already have one — never disables an
/// existing target automatically, since an admin's manual disable must stick. Without this, a
/// fresh Backup deployment shows an empty Overview table until an admin manually triggers a
/// backup for each service by name.
/// </summary>
public class GlobalTargetSyncJob
{
    private readonly BackupDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GlobalTargetSyncJob> _logger;

    public GlobalTargetSyncJob(
        BackupDbContext context,
        IConfiguration configuration,
        ILogger<GlobalTargetSyncJob> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken ct)
    {
        var globalTargets = _configuration.GetSection("Backup:GlobalTargets").Get<List<GlobalTargetOptions>>()
            ?? [];

        if (globalTargets.Count == 0)
        {
            return;
        }

        var existingServiceNames = await _context.BackupTargets
            .Where(t => t.Scope == BackupScope.GlobalService && t.ServiceName != null)
            .Select(t => t.ServiceName!)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingServiceNames, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var globalTarget in globalTargets)
        {
            if (string.IsNullOrWhiteSpace(globalTarget.ServiceName) || existingSet.Contains(globalTarget.ServiceName))
            {
                continue;
            }

            _context.BackupTargets.Add(new BackupTargetEntity
            {
                Scope = BackupScope.GlobalService,
                ServiceName = globalTarget.ServiceName,
                DisplayName = string.IsNullOrWhiteSpace(globalTarget.DisplayName)
                    ? globalTarget.ServiceName
                    : globalTarget.DisplayName,
                IsEnabled = true
            });
            existingSet.Add(globalTarget.ServiceName);
            added++;
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("GlobalTargetSyncJob: added {Count} new global service backup target(s)", added);
        }
    }
}
