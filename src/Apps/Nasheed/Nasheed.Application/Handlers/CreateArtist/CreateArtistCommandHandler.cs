using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.CreateArtist;

public class CreateArtistCommandHandler : IRequestHandler<CreateArtistCommand, ArtistDto>
{
    private readonly IArtistRepository _repository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ILogger<CreateArtistCommandHandler> _logger;
    private readonly string _tenantId;

    public CreateArtistCommandHandler(
        IArtistRepository repository, 
        IFileManagerServiceClient fileManagerClient,
        IConfiguration configuration,
        ILogger<CreateArtistCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerClient = fileManagerClient;
        _tenantId = configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. Nasheed must send tenantId when calling FileManager.");
        _logger = logger;
    }

    public async Task<ArtistDto> Handle(CreateArtistCommand request, CancellationToken cancellationToken)
    {
        var entity = ArtistEntity.Create(request.Name, request.ImageFileId);
        await _repository.AddAsync(entity, cancellationToken);
        _logger.LogInformation("Created Artist with Id {Id}", entity.Id);

        // Mark the image file as in-use (permanent) if provided
        if (request.ImageFileId.HasValue)
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(request.ImageFileId.Value, "Artist", entity.Id.ToString(), true, _tenantId, cancellationToken);
            if (!success)
            {
                _logger.LogWarning("Failed to mark ImageFileId {FileId} as permanent for Artist {ArtistId}", request.ImageFileId.Value, entity.Id);
            }
        }

        return ArtistDto.MapFrom(entity);
    }
}
