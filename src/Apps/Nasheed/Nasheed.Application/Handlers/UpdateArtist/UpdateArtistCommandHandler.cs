using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.UpdateArtist;

public class UpdateArtistCommandHandler : IRequestHandler<UpdateArtistCommand, ArtistDto>
{
    private readonly IArtistRepository _repository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ILogger<UpdateArtistCommandHandler> _logger;
    private readonly string _tenantId;

    public UpdateArtistCommandHandler(
        IArtistRepository repository,
        IFileManagerServiceClient fileManagerClient,
        IConfiguration configuration,
        ILogger<UpdateArtistCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerClient = fileManagerClient;
        _tenantId = configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. Nasheed must send tenantId when calling FileManager.");
        _logger = logger;
    }

    public async Task<ArtistDto> Handle(UpdateArtistCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(LocalizationKeys.Exceptions.ArtistNotFound);

        var oldImageFileId = entity.ImageFileId;
        entity.Update(request.Name, request.ImageFileId);
        await _repository.UpdateAsync(entity, cancellationToken);
        _logger.LogInformation("Updated Artist Id {Id}", entity.Id);

        // Handle file temp status changes
        var newImageFileId = request.ImageFileId;
        
        // If ImageFileId changed
        if (oldImageFileId != newImageFileId)
        {
            // Remove usage row for old image (may set Temp=true if no other usages)
            if (!string.IsNullOrWhiteSpace(oldImageFileId) && int.TryParse(oldImageFileId, out var oldFileId))
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(oldFileId, "Artist", entity.Id.ToString(), false, _tenantId, cancellationToken);
                if (!success)
                {
                    _logger.LogWarning("Failed to remove usage for old ImageFileId {FileId} for Artist {ArtistId}", oldFileId, entity.Id);
                }
            }

            // Add usage row for new image (sets Temp=false)
            if (!string.IsNullOrWhiteSpace(newImageFileId) && int.TryParse(newImageFileId, out var newFileId))
            {
                var success = await _fileManagerClient.ChangeTempStatusAsync(newFileId, "Artist", entity.Id.ToString(), true, _tenantId, cancellationToken);
                if (!success)
                {
                    _logger.LogWarning("Failed to add usage for new ImageFileId {FileId} for Artist {ArtistId}", newFileId, entity.Id);
                }
            }
        }

        return ArtistDto.MapFrom(entity);
    }
}
