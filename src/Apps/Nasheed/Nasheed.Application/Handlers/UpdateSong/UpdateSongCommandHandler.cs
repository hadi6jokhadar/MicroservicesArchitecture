using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.UpdateSong;

public class UpdateSongCommandHandler : IRequestHandler<UpdateSongCommand, SongDto>
{
    private readonly ISongRepository _repository;
    private readonly ILogger<UpdateSongCommandHandler> _logger;

    public UpdateSongCommandHandler(ISongRepository repository, ILogger<UpdateSongCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SongDto> Handle(UpdateSongCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Song with Id '{request.Id}' not found.");

        entity.UpdateTitle(request.Title);

        if (request.ArtistId.HasValue && request.ArtistId != entity.ArtistId)
        {
            throw new InvalidOperationException("Changing artist on an existing song is not supported. Archive the song and create a new one.");
        }

        await _repository.UpdateAsync(entity, cancellationToken);
        _logger.LogInformation("Updated Song Id {Id}", entity.Id);
        return SongDto.MapFrom(entity);
    }
}
