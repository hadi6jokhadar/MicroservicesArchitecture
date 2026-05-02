using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.DeleteArtist;

public class DeleteArtistCommandHandler : IRequestHandler<DeleteArtistCommand, bool>
{
    private readonly IArtistRepository _repository;
    private readonly ILogger<DeleteArtistCommandHandler> _logger;

    public DeleteArtistCommandHandler(IArtistRepository repository, ILogger<DeleteArtistCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteArtistCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Artist with Id '{request.Id}' not found.");

        await _repository.DeleteAsync(entity, cancellationToken);
        _logger.LogInformation("Deleted Artist Id {Id}", entity.Id);
        return true;
    }
}
