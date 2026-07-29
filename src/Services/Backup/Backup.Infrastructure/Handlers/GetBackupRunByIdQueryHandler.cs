using Backup.Application.DTOs;
using Backup.Application.Queries;
using Backup.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>See <see cref="UpdateBackupTargetCommandHandler"/> for why this lives in Infrastructure.</summary>
public class GetBackupRunByIdQueryHandler : IRequestHandler<GetBackupRunByIdQuery, BackupRunDto>
{
    private readonly BackupDbContext _context;

    public GetBackupRunByIdQueryHandler(BackupDbContext context)
    {
        _context = context;
    }

    public async Task<BackupRunDto> Handle(GetBackupRunByIdQuery request, CancellationToken cancellationToken)
    {
        var run = await _context.BackupRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.BackupRunNotFound);

        return BackupRunDto.MapFrom(run);
    }
}
