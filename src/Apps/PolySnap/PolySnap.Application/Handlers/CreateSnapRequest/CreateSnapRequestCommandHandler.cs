using MediatR;
using Microsoft.Extensions.Logging;
using PolySnap.Application.Commands;
using PolySnap.Application.DTOs;
using PolySnap.Domain.Entities;
using PolySnap.Domain.Interfaces;

namespace PolySnap.Application.Handlers.CreateSnapRequest;

public class CreateSnapRequestCommandHandler : IRequestHandler<CreateSnapRequestCommand, SnapRequestDto>
{
    private readonly ISnapRequestRepository _repository;
    private readonly ILogger<CreateSnapRequestCommandHandler> _logger;

    public CreateSnapRequestCommandHandler(
        ISnapRequestRepository repository,
        ILogger<CreateSnapRequestCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SnapRequestDto> Handle(CreateSnapRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = SnapRequestEntity.Create(request.Name, request.RawGeometryGeoJson, request.Threshold);
        await _repository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("Created SnapRequest with Id {Id}", entity.Id);
        return SnapRequestDto.MapFrom(entity);
    }
}
