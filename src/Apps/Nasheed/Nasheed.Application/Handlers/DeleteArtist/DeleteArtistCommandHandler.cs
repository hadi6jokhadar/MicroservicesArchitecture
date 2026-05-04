using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.DeleteArtist;

public class DeleteArtistCommandHandler : IRequestHandler<DeleteArtistCommand, bool>
{
    private readonly IArtistRepository _repository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ILogger<DeleteArtistCommandHandler> _logger;
    private readonly string _tenantId;

    public DeleteArtistCommandHandler(
        IArtistRepository repository,
        IFileManagerServiceClient fileManagerClient,
        IConfiguration configuration,
        ILogger<DeleteArtistCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerClient = fileManagerClient;
        _tenantId = configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. Nasheed must send tenantId when calling FileManager.");
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteArtistCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Artist with Id '{request.Id}' not found.");

        await _repository.DeleteAsync(entity, cancellationToken);
        _logger.LogInformation("Deleted Artist Id {Id}", entity.Id);

        // Remove file usage row (will set Temp=true if no other usages)
        if (!string.IsNullOrWhiteSpace(entity.ImageFileId) && int.TryParse(entity.ImageFileId, out var fileId))
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(fileId, "Artist", entity.Id.ToString(), false, _tenantId, cancellationToken);
            if (!success)
            {
                _logger.LogWarning("Failed to mark ImageFileId {FileId} as temporary after deleting Artist {ArtistId}", fileId, entity.Id);
            }
        }

        return true;
    }
}
