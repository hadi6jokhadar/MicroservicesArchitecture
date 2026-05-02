using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Queries;

public record GetArtistByIdQuery(int Id) : IRequest<ArtistDto?>;

public record GetArtistListQuery(
    string? TextFilter = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PaginatedList<ArtistDto>>;
