using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetIngestionJobList;

public class GetIngestionJobListQueryHandler : IRequestHandler<GetIngestionJobListQuery, PaginatedList<IngestionJobDto>>
{
    private readonly ISongIngestionJobRepository _repository;

    public GetIngestionJobListQueryHandler(ISongIngestionJobRepository repository) => _repository = repository;

    public async Task<PaginatedList<IngestionJobDto>> Handle(GetIngestionJobListQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetAllAsync(
            request.SongId,
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return new PaginatedList<IngestionJobDto>
        {
            Items = items.Select(IngestionJobDto.MapFrom).ToList(),
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
