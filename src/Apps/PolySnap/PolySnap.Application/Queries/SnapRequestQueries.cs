using MediatR;
using PolySnap.Application.DTOs;

namespace PolySnap.Application.Queries;

public record GetSnapRequestByIdQuery(int Id) : IRequest<SnapRequestDto?>;

public record GetSnapRequestListQuery(
    string? TextFilter = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<SnapRequestDto>>;
