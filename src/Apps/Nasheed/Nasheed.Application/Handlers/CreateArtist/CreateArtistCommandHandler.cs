using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.CreateArtist;

public class CreateArtistCommandHandler : IRequestHandler<CreateArtistCommand, ArtistDto>
{
    private readonly IArtistRepository _repository;
    private readonly ILogger<CreateArtistCommandHandler> _logger;

    public CreateArtistCommandHandler(IArtistRepository repository, ILogger<CreateArtistCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ArtistDto> Handle(CreateArtistCommand request, CancellationToken cancellationToken)
    {
        var entity = ArtistEntity.Create(request.Name, request.ImageFileId);
        await _repository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("Created Artist with Id {Id}", entity.Id);
        return ArtistDto.MapFrom(entity);
    }
}
