using Backup.Application.DTOs;
using Backup.Application.Queries;
using Backup.Domain.Entities;
using Backup.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>
/// See <see cref="UpdateBackupTargetCommandHandler"/> for why this lives in Infrastructure.
/// The "latest run per target" reduction is done in memory after materializing runs ordered by
/// Created descending — EF Core's Npgsql provider does not reliably translate a
/// GroupBy().Select(g => g.OrderBy().First()) "greatest-n-per-group" query, so this avoids a
/// runtime translation failure.
/// </summary>
public class GetBackupSummaryQueryHandler : IRequestHandler<GetBackupSummaryQuery, List<BackupSummaryDto>>
{
    private readonly BackupDbContext _context;

    public GetBackupSummaryQueryHandler(BackupDbContext context)
    {
        _context = context;
    }

    public async Task<List<BackupSummaryDto>> Handle(GetBackupSummaryQuery request, CancellationToken cancellationToken)
    {
        var targets = await _context.BackupTargets
            .AsNoTracking()
            .OrderBy(t => t.Scope)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);

        var runs = await _context.BackupRuns
            .AsNoTracking()
            .Where(r => r.BackupTargetId != null)
            .OrderByDescending(r => r.Created)
            .ToListAsync(cancellationToken);

        var latestByTarget = new Dictionary<int, BackupRunEntity>();
        foreach (var run in runs)
        {
            var targetId = run.BackupTargetId!.Value;
            if (!latestByTarget.ContainsKey(targetId))
            {
                latestByTarget[targetId] = run;
            }
        }

        return targets
            .Select(target => BackupSummaryDto.MapFrom(
                target,
                latestByTarget.GetValueOrDefault(target.Id)))
            .ToList();
    }
}
