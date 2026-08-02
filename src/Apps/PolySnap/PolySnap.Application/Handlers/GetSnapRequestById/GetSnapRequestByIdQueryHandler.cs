using MediatR;
using PolySnap.Application.DTOs;
using PolySnap.Application.Queries;
using PolySnap.Domain.Interfaces;

namespace PolySnap.Application.Handlers.GetSnapRequestById;

public class GetSnapRequestByIdQueryHandler : IRequestHandler<GetSnapRequestByIdQuery, SnapRequestDto?>
{
    private readonly ISnapRequestRepository _repository;

    public GetSnapRequestByIdQueryHandler(ISnapRequestRepository repository) => _repository = repository;

    public async Task<SnapRequestDto?> Handle(GetSnapRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity == null ? null : SnapRequestDto.MapFrom(entity);
    }
}
