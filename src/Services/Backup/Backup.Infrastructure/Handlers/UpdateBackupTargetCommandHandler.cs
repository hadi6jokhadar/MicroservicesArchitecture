using Backup.Application.Commands;
using Backup.Application.DTOs;
using Backup.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>
/// Lives in Infrastructure (not Application) so it can talk to <see cref="BackupDbContext"/>
/// directly — mirrors the shared <c>GetAuditLogsQueryHandler&lt;TDbContext&gt;</c> convention.
/// Putting DbContext-consuming handlers in Application would create a circular project
/// reference, since Backup.Infrastructure already depends on Backup.Application.
/// </summary>
public class UpdateBackupTargetCommandHandler : IRequestHandler<UpdateBackupTargetCommand, BackupTargetDto>
{
    private readonly BackupDbContext _context;

    public UpdateBackupTargetCommandHandler(BackupDbContext context)
    {
        _context = context;
    }

    public async Task<BackupTargetDto> Handle(UpdateBackupTargetCommand request, CancellationToken cancellationToken)
    {
        var target = await _context.BackupTargets.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.BackupTargetNotFound);

        if (request.IsEnabled.HasValue)
        {
            target.IsEnabled = request.IsEnabled.Value;
        }

        if (request.RetentionDays.HasValue)
        {
            target.RetentionDays = request.RetentionDays.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return BackupTargetDto.MapFrom(target);
    }
}
