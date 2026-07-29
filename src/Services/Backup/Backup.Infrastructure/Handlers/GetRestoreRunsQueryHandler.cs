using Backup.Application.DTOs;
using Backup.Application.Queries;
using Backup.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>See <see cref="UpdateBackupTargetCommandHandler"/> for why this lives in Infrastructure.</summary>
public class GetRestoreRunsQueryHandler : IRequestHandler<GetRestoreRunsQuery, PaginatedList<RestoreRunDto>>
{
    private readonly BackupDbContext _context;

    public GetRestoreRunsQueryHandler(BackupDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<RestoreRunDto>> Handle(GetRestoreRunsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.RestoreRuns.AsNoTracking().OrderByDescending(r => r.Created);

        var pageSize = Math.Min(Math.Max(request.PageSize, 1), 100);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(RestoreRunDto.MapFrom).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedList<RestoreRunDto>(dtos, totalCount, pageNumber, totalPages);
    }
}
