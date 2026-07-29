using Backup.Application.Commands;
using Backup.Application.DTOs;
using Backup.Application.Interfaces;
using Backup.Domain.Entities;
using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using Hangfire;
using IhsanDev.Shared.Infrastructure.Services.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>See <see cref="UpdateBackupTargetCommandHandler"/> for why this lives in Infrastructure.</summary>
public class TriggerBackupCommandHandler : IRequestHandler<TriggerBackupCommand, BackupRunDto>
{
    private readonly BackupDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public TriggerBackupCommandHandler(
        BackupDbContext context,
        ICurrentUserService currentUserService,
        IBackgroundJobClient backgroundJobClient)
    {
        _context = context;
        _currentUserService = currentUserService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<BackupRunDto> Handle(TriggerBackupCommand request, CancellationToken cancellationToken)
    {
        var target = request.Scope == BackupScope.GlobalService
            ? await _context.BackupTargets.FirstOrDefaultAsync(
                t => t.Scope == BackupScope.GlobalService && t.ServiceName == request.ServiceName, cancellationToken)
            : await _context.BackupTargets.FirstOrDefaultAsync(
                t => t.Scope == BackupScope.Tenant && t.TenantId == request.TenantId, cancellationToken);

        if (target == null)
        {
            target = new BackupTargetEntity
            {
                Scope = request.Scope,
                ServiceName = request.Scope == BackupScope.GlobalService ? request.ServiceName : null,
                TenantId = request.Scope == BackupScope.Tenant ? request.TenantId : null,
                DisplayName = request.Scope == BackupScope.GlobalService ? request.ServiceName! : request.TenantId!,
                IsEnabled = true
            };
            _context.BackupTargets.Add(target);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var run = new BackupRunEntity
        {
            BackupTargetId = target.Id,
            Scope = target.Scope,
            ServiceName = target.ServiceName,
            TenantId = target.TenantId,
            TriggerType = BackupTriggerType.Manual,
            Status = BackupRunStatus.Pending,
            TriggeredByUserId = int.TryParse(_currentUserService.UserId, out var userId) ? userId : null,
            TriggeredByEmail = _currentUserService.Email
        };

        _context.BackupRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<IRunBackupJob>(job => job.ExecuteAsync(run.Id, CancellationToken.None));

        return BackupRunDto.MapFrom(run);
    }
}
