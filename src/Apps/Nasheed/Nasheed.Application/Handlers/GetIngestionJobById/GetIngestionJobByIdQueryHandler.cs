using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetIngestionJobById;

public class GetIngestionJobByIdQueryHandler : IRequestHandler<GetIngestionJobByIdQuery, IngestionJobDto?>
{
    private readonly ISongIngestionJobRepository _repository;
    private readonly ISongRepository _songRepository;

    public GetIngestionJobByIdQueryHandler(ISongIngestionJobRepository repository, ISongRepository songRepository)
    {
        _repository = repository;
        _songRepository = songRepository;
    }

    public async Task<IngestionJobDto?> Handle(GetIngestionJobByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            return null;
        }

        var song = await _songRepository.GetByIdAsync(entity.SongId, cancellationToken);
        return IngestionJobDto.MapFrom(entity, song?.Title);
    }
}
