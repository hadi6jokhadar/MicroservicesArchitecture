using Backup.Application.DTOs;
using Backup.Application.Queries;
using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>See <see cref="UpdateBackupTargetCommandHandler"/> for why this lives in Infrastructure.</summary>
public class GetBackupTargetsQueryHandler : IRequestHandler<GetBackupTargetsQuery, List<BackupTargetDto>>
{
    private readonly BackupDbContext _context;

    public GetBackupTargetsQueryHandler(BackupDbContext context)
    {
        _context = context;
    }

    public async Task<List<BackupTargetDto>> Handle(GetBackupTargetsQuery request, CancellationToken cancellationToken)
    {
        var targets = await _context.BackupTargets
            .AsNoTracking()
            .OrderBy(t => t.Scope)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);

        return targets.Select(BackupTargetDto.MapFrom).ToList();
    }
}
