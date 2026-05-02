using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetArtistList;

public class GetArtistListQueryHandler : IRequestHandler<GetArtistListQuery, PaginatedList<ArtistDto>>
{
    private readonly IArtistRepository _repository;

    public GetArtistListQueryHandler(IArtistRepository repository) => _repository = repository;

    public async Task<PaginatedList<ArtistDto>> Handle(GetArtistListQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetAllAsync(
            request.TextFilter, request.PageNumber, request.PageSize, cancellationToken);

        return new PaginatedList<ArtistDto>
        {
            Items = items.Select(ArtistDto.MapFrom).ToList(),
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
