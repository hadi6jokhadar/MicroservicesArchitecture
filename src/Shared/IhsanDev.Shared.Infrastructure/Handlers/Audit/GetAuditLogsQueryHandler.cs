using IhsanDev.Shared.Application.Audit;
using IhsanDev.Shared.Application.Common.Models;
using IhsanDev.Shared.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IhsanDev.Shared.Infrastructure.Handlers.Audit;

public class GetAuditLogsQueryHandler<TDbContext> : IRequestHandler<GetAuditLogsQuery, PaginatedList<AuditLogDto>>
    where TDbContext : BaseDbContext
{
    private readonly TDbContext _context;

    public GetAuditLogsQueryHandler(TDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.TenantId))
            query = query.Where(x => x.TenantId == request.TenantId);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(x => x.EntityType == request.EntityType);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(x => x.Action.Contains(request.Action));

        if (!string.IsNullOrWhiteSpace(request.UserId))
            query = query.Where(x => x.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.UserEmail))
            query = query.Where(x => x.UserEmail != null && x.UserEmail.Contains(request.UserEmail));

        if (request.FromDate.HasValue)
            query = query.Where(x => x.OccurredAt >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(x => x.OccurredAt <= request.ToDate.Value);

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "action"     => request.SortDesc ? query.OrderByDescending(x => x.Action)     : query.OrderBy(x => x.Action),
            "entitytype" => request.SortDesc ? query.OrderByDescending(x => x.EntityType) : query.OrderBy(x => x.EntityType),
            "entityid"   => request.SortDesc ? query.OrderByDescending(x => x.EntityId)   : query.OrderBy(x => x.EntityId),
            "userid"     => request.SortDesc ? query.OrderByDescending(x => x.UserId)     : query.OrderBy(x => x.UserId),
            _            => request.SortDesc ? query.OrderByDescending(x => x.OccurredAt) : query.OrderBy(x => x.OccurredAt)
        };

        var pageSize = Math.Min(Math.Max(request.PageSize, 1), 100);
        var page = Math.Max(request.Page, 1);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(AuditLogDto.MapFrom).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PaginatedList<AuditLogDto>(dtos, totalCount, page, totalPages);
    }
}
