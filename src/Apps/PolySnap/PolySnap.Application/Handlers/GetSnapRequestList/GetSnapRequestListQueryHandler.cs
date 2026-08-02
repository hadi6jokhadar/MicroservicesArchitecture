using MediatR;
using PolySnap.Application.DTOs;
using PolySnap.Application.Queries;
using PolySnap.Domain.Interfaces;

namespace PolySnap.Application.Handlers.GetSnapRequestList;

public class GetSnapRequestListQueryHandler : IRequestHandler<GetSnapRequestListQuery, PaginatedList<SnapRequestDto>>
{
    private readonly ISnapRequestRepository _repository;

    public GetSnapRequestListQueryHandler(ISnapRequestRepository repository) => _repository = repository;

    public async Task<PaginatedList<SnapRequestDto>> Handle(GetSnapRequestListQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetAllAsync(
            request.TextFilter, request.PageNumber, request.PageSize, cancellationToken);

        return new PaginatedList<SnapRequestDto>
        {
            Items = items.Select(SnapRequestDto.MapFrom).ToList(),
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
