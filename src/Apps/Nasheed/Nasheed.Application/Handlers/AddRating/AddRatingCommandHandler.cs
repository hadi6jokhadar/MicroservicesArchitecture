using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.AddRating;

public class AddRatingCommandHandler : IRequestHandler<AddRatingCommand, RatingDto>
{
    private readonly IRatingRepository _repository;
    private readonly ILogger<AddRatingCommandHandler> _logger;

    public AddRatingCommandHandler(IRatingRepository repository, ILogger<AddRatingCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RatingDto> Handle(AddRatingCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAsync(request.UserId, request.SongId, cancellationToken);
        if (existing != null)
        {
            existing.Update(request.Value);
            await _repository.UpdateAsync(existing, cancellationToken);
            _logger.LogInformation("User {UserId} updated rating for Song {SongId} to {Value}", request.UserId, request.SongId, request.Value);
            return RatingDto.MapFrom(existing);
        }

        var entity = RatingEntity.Create(request.UserId, request.SongId, request.Value);
        await _repository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("User {UserId} rated Song {SongId} with {Value}", request.UserId, request.SongId, request.Value);
        return RatingDto.MapFrom(entity);
    }
}
