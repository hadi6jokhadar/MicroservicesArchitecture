using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.DeleteSong;

public class DeleteSongCommandHandler : IRequestHandler<DeleteSongCommand, bool>
{
    private readonly ISongRepository _songRepository;
    private readonly IArtistRepository _artistRepository;
    private readonly ILogger<DeleteSongCommandHandler> _logger;

    public DeleteSongCommandHandler(ISongRepository songRepository, IArtistRepository artistRepository, ILogger<DeleteSongCommandHandler> logger)
    {
        _songRepository = songRepository;
        _artistRepository = artistRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteSongCommand request, CancellationToken cancellationToken)
    {
        var entity = await _songRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Song with Id '{request.Id}' not found.");

        await _songRepository.DeleteAsync(entity, cancellationToken);

        var artist = await _artistRepository.GetByIdAsync(entity.ArtistId, cancellationToken);
        if (artist != null)
        {
            artist.DecrementSongCount();
            await _artistRepository.UpdateAsync(artist, cancellationToken);
        }

        _logger.LogInformation("Deleted Song Id {Id}", entity.Id);
        return true;
    }
}
