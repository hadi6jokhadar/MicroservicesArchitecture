using Backup.Application.DTOs;
using IhsanDev.Shared.Application.Common.Models;
using MediatR;

namespace Backup.Application.Queries;

/// <summary>
/// Paginated, filterable list of backup runs for the admin "backup history" screen.
/// <paramref name="Scope"/> and <paramref name="Status"/> are matched against the
/// <c>BackupScope</c>/<c>BackupRunStatus</c> enum names (case-insensitive); an unrecognized value
/// is treated as "no filter" rather than an error.
/// </summary>
public record GetBackupRunsQuery(
    string? Scope,
    string? ServiceName,
    string? TenantId,
    string? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    int PageNumber,
    int PageSize) : IRequest<PaginatedList<BackupRunDto>>;
