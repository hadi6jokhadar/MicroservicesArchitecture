using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetIngestionJobById;

public class GetIngestionJobByIdQueryHandler : IRequestHandler<GetIngestionJobByIdQuery, IngestionJobDto?>
{
    private readonly ISongIngestionJobRepository _repository;

    public GetIngestionJobByIdQueryHandler(ISongIngestionJobRepository repository) => _repository = repository;

    public async Task<IngestionJobDto?> Handle(GetIngestionJobByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity == null ? null : IngestionJobDto.MapFrom(entity);
    }
}
