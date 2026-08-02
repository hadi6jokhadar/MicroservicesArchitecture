using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using PolySnap.Application.Commands;
using PolySnap.Domain.Entities;
using PolySnap.Domain.Interfaces;

namespace PolySnap.Application.Handlers.DeleteSnapRequest;

public class DeleteSnapRequestCommandHandler : IRequestHandler<DeleteSnapRequestCommand, bool>
{
    private readonly ISnapRequestRepository _repository;
    private readonly ILogger<DeleteSnapRequestCommandHandler> _logger;

    public DeleteSnapRequestCommandHandler(
        ISnapRequestRepository repository,
        ILogger<DeleteSnapRequestCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteSnapRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"{nameof(SnapRequestEntity)} with Id '{request.Id}' not found.");

        await _repository.DeleteAsync(entity, cancellationToken);
        _logger.LogInformation("Deleted SnapRequest Id {Id}", entity.Id);
        return true;
    }
}
