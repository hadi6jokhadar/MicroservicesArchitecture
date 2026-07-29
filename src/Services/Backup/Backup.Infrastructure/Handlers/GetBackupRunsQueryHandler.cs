using Backup.Application.DTOs;
using Backup.Application.Queries;
using Backup.Domain.Enums;
using Backup.Infrastructure.Persistence;
using IhsanDev.Shared.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Handlers;

/// <summary>See <see cref="UpdateBackupTargetCommandHandler"/> for why this lives in Infrastructure.</summary>
public class GetBackupRunsQueryHandler : IRequestHandler<GetBackupRunsQuery, PaginatedList<BackupRunDto>>
{
    private readonly BackupDbContext _context;

    public GetBackupRunsQueryHandler(BackupDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<BackupRunDto>> Handle(GetBackupRunsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.BackupRuns.AsNoTracking();

        if (Enum.TryParse<BackupScope>(request.Scope, ignoreCase: true, out var scope))
        {
            query = query.Where(r => r.Scope == scope);
        }

        if (!string.IsNullOrWhiteSpace(request.ServiceName))
        {
            query = query.Where(r => r.ServiceName == request.ServiceName);
        }

        if (!string.IsNullOrWhiteSpace(request.TenantId))
        {
            query = query.Where(r => r.TenantId == request.TenantId);
        }

        if (Enum.TryParse<BackupRunStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(r => r.Created >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(r => r.Created <= request.ToDate.Value);
        }

        query = query.OrderByDescending(r => r.Created);

        var pageSize = Math.Min(Math.Max(request.PageSize, 1), 100);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(BackupRunDto.MapFrom).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedList<BackupRunDto>(dtos, totalCount, pageNumber, totalPages);
    }
}
