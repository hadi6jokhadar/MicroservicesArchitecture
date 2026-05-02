using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.UpdateArtist;

public class UpdateArtistCommandHandler : IRequestHandler<UpdateArtistCommand, ArtistDto>
{
    private readonly IArtistRepository _repository;
    private readonly ILogger<UpdateArtistCommandHandler> _logger;

    public UpdateArtistCommandHandler(IArtistRepository repository, ILogger<UpdateArtistCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ArtistDto> Handle(UpdateArtistCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Artist with Id '{request.Id}' not found.");

        entity.Update(request.Name, request.ImageFileId);
        await _repository.UpdateAsync(entity, cancellationToken);
        _logger.LogInformation("Updated Artist Id {Id}", entity.Id);
        return ArtistDto.MapFrom(entity);
    }
}
