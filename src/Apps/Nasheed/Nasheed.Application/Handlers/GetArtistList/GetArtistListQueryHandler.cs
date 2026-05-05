using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetArtistList;

public class GetArtistListQueryHandler : IRequestHandler<GetArtistListQuery, PaginatedList<ArtistDto>>
{
    private readonly IArtistRepository _repository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;

    public GetArtistListQueryHandler(IArtistRepository repository, NasheedFileManagerHelper fileManagerHelper)
    {
        _repository = repository;
        _fileManagerHelper = fileManagerHelper;
    }

    public async Task<PaginatedList<ArtistDto>> Handle(GetArtistListQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetAllAsync(
            request.TextFilter, request.PageNumber, request.PageSize, cancellationToken);

        var dtos = items.Select(ArtistDto.MapFrom).ToList();
        await _fileManagerHelper.EnrichArtistsWithImagesAsync(dtos, cancellationToken);

        return new PaginatedList<ArtistDto>
        {
            Items = dtos,
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
