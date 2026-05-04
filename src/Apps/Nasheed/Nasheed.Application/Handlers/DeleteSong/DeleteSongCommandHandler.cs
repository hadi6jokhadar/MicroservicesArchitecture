using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.DeleteSong;

public class DeleteSongCommandHandler : IRequestHandler<DeleteSongCommand, bool>
{
    private readonly ISongRepository _songRepository;
    private readonly IArtistRepository _artistRepository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly ILogger<DeleteSongCommandHandler> _logger;
    private readonly string _tenantId;

    public DeleteSongCommandHandler(
        ISongRepository songRepository,
        IArtistRepository artistRepository,
        IFileManagerServiceClient fileManagerClient,
        IConfiguration configuration,
        ILogger<DeleteSongCommandHandler> logger)
    {
        _songRepository = songRepository;
        _artistRepository = artistRepository;
        _fileManagerClient = fileManagerClient;
        _tenantId = configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. Nasheed must send tenantId when calling FileManager.");
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

        // Remove file usage row (will set Temp=true if no other usages)
        if (!string.IsNullOrWhiteSpace(entity.FileId) && int.TryParse(entity.FileId, out var fileId))
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(fileId, "Song", entity.Id.ToString(), false, _tenantId, cancellationToken);
            if (!success)
            {
                _logger.LogWarning("Failed to mark FileId {FileId} as temporary after deleting Song {SongId}", fileId, entity.Id);
            }
        }

        return true;
    }
}
