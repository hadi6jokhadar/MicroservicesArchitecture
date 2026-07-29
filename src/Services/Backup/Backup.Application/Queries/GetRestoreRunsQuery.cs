using Backup.Application.DTOs;
using IhsanDev.Shared.Application.Common.Models;
using MediatR;

namespace Backup.Application.Queries;

public record GetRestoreRunsQuery(int PageNumber, int PageSize) : IRequest<PaginatedList<RestoreRunDto>>;
