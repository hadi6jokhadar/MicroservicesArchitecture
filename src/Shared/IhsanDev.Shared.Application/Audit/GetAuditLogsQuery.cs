using IhsanDev.Shared.Application.Common.Models;
using MediatR;

namespace IhsanDev.Shared.Application.Audit;

public record GetAuditLogsQuery(
    string? TenantId = null,
    string? EntityType = null,
    string? Action = null,
    string? UserId = null,
    string? UserEmail = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "OccurredAt",
    bool SortDesc = true
) : IRequest<PaginatedList<AuditLogDto>>;
