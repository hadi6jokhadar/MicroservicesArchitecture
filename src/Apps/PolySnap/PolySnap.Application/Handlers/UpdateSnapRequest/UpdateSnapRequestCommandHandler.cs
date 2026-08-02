using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using PolySnap.Application.Commands;
using PolySnap.Application.DTOs;
using PolySnap.Domain.Entities;
using PolySnap.Domain.Interfaces;

namespace PolySnap.Application.Handlers.UpdateSnapRequest;

public class UpdateSnapRequestCommandHandler : IRequestHandler<UpdateSnapRequestCommand, SnapRequestDto>
{
    private readonly ISnapRequestRepository _repository;
    private readonly ILogger<UpdateSnapRequestCommandHandler> _logger;

    public UpdateSnapRequestCommandHandler(
        ISnapRequestRepository repository,
        ILogger<UpdateSnapRequestCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SnapRequestDto> Handle(UpdateSnapRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"{nameof(SnapRequestEntity)} with Id '{request.Id}' not found.");

        entity.Update(request.Name, request.RawGeometryGeoJson, request.SnappedGeometryGeoJson, request.Threshold);
        await _repository.UpdateAsync(entity, cancellationToken);
        _logger.LogInformation("Updated SnapRequest Id {Id}", entity.Id);
        return SnapRequestDto.MapFrom(entity);
    }
}
