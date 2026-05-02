using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.RemoveFavorite;

public class RemoveFavoriteCommandHandler : IRequestHandler<RemoveFavoriteCommand, bool>
{
    private readonly IFavoriteRepository _repository;
    private readonly ILogger<RemoveFavoriteCommandHandler> _logger;

    public RemoveFavoriteCommandHandler(IFavoriteRepository repository, ILogger<RemoveFavoriteCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(RemoveFavoriteCommand request, CancellationToken cancellationToken)
    {
        var favorite = await _repository.GetAsync(request.UserId, request.SongId, cancellationToken);
        if (favorite == null) return false;
        await _repository.RemoveAsync(favorite, cancellationToken);
        _logger.LogInformation("User {UserId} removed Song {SongId} from favorites", request.UserId, request.SongId);
        return true;
    }
}
