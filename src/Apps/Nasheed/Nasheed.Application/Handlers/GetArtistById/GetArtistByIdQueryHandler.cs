using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetArtistById;

public class GetArtistByIdQueryHandler : IRequestHandler<GetArtistByIdQuery, ArtistDto?>
{
    private readonly IArtistRepository _repository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;

    public GetArtistByIdQueryHandler(IArtistRepository repository, NasheedFileManagerHelper fileManagerHelper)
    {
        _repository = repository;
        _fileManagerHelper = fileManagerHelper;
    }

    public async Task<ArtistDto?> Handle(GetArtistByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null) return null;

        var dto = ArtistDto.MapFrom(entity);
        await _fileManagerHelper.EnrichArtistWithImageAsync(dto, cancellationToken);
        return dto;
    }
}
