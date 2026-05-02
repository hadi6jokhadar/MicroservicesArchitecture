using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.AddFavorite;

public class AddFavoriteCommandHandler : IRequestHandler<AddFavoriteCommand, FavoriteDto>
{
    private readonly IFavoriteRepository _repository;
    private readonly ILogger<AddFavoriteCommandHandler> _logger;

    public AddFavoriteCommandHandler(IFavoriteRepository repository, ILogger<AddFavoriteCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FavoriteDto> Handle(AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAsync(request.UserId, request.SongId, cancellationToken);
        if (existing != null) return FavoriteDto.MapFrom(existing);

        var entity = FavoriteEntity.Create(request.UserId, request.SongId);
        await _repository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("User {UserId} added Song {SongId} to favorites", request.UserId, request.SongId);
        return FavoriteDto.MapFrom(entity);
    }
}
