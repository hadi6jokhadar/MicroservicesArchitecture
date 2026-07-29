using Backup.Application.Commands;
using Backup.Application.DTOs;
using Backup.Application.Interfaces;
using Backup.Domain.Entities;
using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using Hangfire;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>See <see cref="UpdateBackupTargetCommandHandler"/> for why this lives in Infrastructure.</summary>
public class TriggerRestoreCommandHandler : IRequestHandler<TriggerRestoreCommand, RestoreRunDto>
{
    private readonly BackupDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public TriggerRestoreCommandHandler(
        BackupDbContext context,
        ICurrentUserService currentUserService,
        IBackgroundJobClient backgroundJobClient)
    {
        _context = context;
        _currentUserService = currentUserService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<RestoreRunDto> Handle(TriggerRestoreCommand request, CancellationToken cancellationToken)
    {
        var backupRun = await _context.BackupRuns.FirstOrDefaultAsync(r => r.Id == request.BackupRunId, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.BackupRunNotFound);

        var restoreRun = new RestoreRunEntity
        {
            BackupRunId = backupRun.Id,
            Status = BackupRunStatus.Pending,
            TargetConnectionOverride = request.TargetConnectionOverride,
            TriggeredByUserId = int.TryParse(_currentUserService.UserId, out var userId) ? userId : null,
            TriggeredByEmail = _currentUserService.Email
        };

        _context.RestoreRuns.Add(restoreRun);
        await _context.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<IRunRestoreJob>(job => job.ExecuteAsync(restoreRun.Id, CancellationToken.None));

        return RestoreRunDto.MapFrom(restoreRun);
    }
}
