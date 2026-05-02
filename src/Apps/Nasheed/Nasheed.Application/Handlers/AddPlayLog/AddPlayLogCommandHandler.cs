using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.AddPlayLog;

public class AddPlayLogCommandHandler : IRequestHandler<AddPlayLogCommand, bool>
{
    private readonly IPlayLogRepository _repository;
    private readonly ILogger<AddPlayLogCommandHandler> _logger;

    public AddPlayLogCommandHandler(IPlayLogRepository repository, ILogger<AddPlayLogCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(AddPlayLogCommand request, CancellationToken cancellationToken)
    {
        var entity = PlayLogEntity.Create(request.SongId, request.UserId);
        await _repository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("Logged play for Song {SongId} by User {UserId}", request.SongId, request.UserId);
        return true;
    }
}
