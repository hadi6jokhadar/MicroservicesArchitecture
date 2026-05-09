using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using IhsanDev.Shared.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.CreateSong;

public class CreateSongCommandHandler : IRequestHandler<CreateSongCommand, SongDto>
{
    private readonly ISongRepository _songRepository;
    private readonly IArtistRepository _artistRepository;
    private readonly ISongIngestionJobRepository _ingestionJobRepository;
    private readonly IFileManagerServiceClient _fileManagerClient;
    private readonly NasheedFileManagerHelper _fileManagerHelper;
    private readonly ILogger<CreateSongCommandHandler> _logger;
    private readonly string _tenantId;

    public CreateSongCommandHandler(
        ISongRepository songRepository,
        IArtistRepository artistRepository,
        ISongIngestionJobRepository ingestionJobRepository,
        IFileManagerServiceClient fileManagerClient,
        NasheedFileManagerHelper fileManagerHelper,
        IConfiguration configuration,
        ILogger<CreateSongCommandHandler> logger)
    {
        _songRepository = songRepository;
        _artistRepository = artistRepository;
        _ingestionJobRepository = ingestionJobRepository;
        _fileManagerClient = fileManagerClient;
        _fileManagerHelper = fileManagerHelper;
        _tenantId = configuration["MultiTenancy:TenantId"]
            ?? throw new InvalidOperationException(
                "MultiTenancy:TenantId is not configured. Nasheed must send tenantId when calling FileManager.");
        _logger = logger;
    }

    public async Task<SongDto> Handle(CreateSongCommand request, CancellationToken cancellationToken)
    {
        ArtistEntity? artist = null;
        if (request.ArtistId.HasValue)
        {
            artist = await _artistRepository.GetByIdAsync(request.ArtistId.Value, cancellationToken)
                ?? throw new NotFoundException(LocalizationKeys.Exceptions.ArtistNotFound);
        }

        var song = SongEntity.Create(request.ArtistId, request.Title, request.FileId);
        song.UpdateLegalComplianceFromAi(request.CopyrightRiskLevel, request.ContentSafetyFlag, request.RiskReason);
        await _songRepository.AddAsync(song, cancellationToken);

        // Enqueue ingestion job
        song.SetState(SongState.InQueue);
        await _songRepository.UpdateAsync(song, cancellationToken);

        var job = SongIngestionJobEntity.Create(song.Id, request.FileId, IngestionJobType.FullPipeline);
        await _ingestionJobRepository.AddAsync(job, cancellationToken);

        // Increment artist song count only when song is linked to an artist.
        if (artist != null)
        {
            artist.IncrementSongCount();
            await _artistRepository.UpdateAsync(artist, cancellationToken);
        }

        _logger.LogInformation("Created Song Id {SongId} with ingestion job Id {JobId}", song.Id, job.Id);

        // Mark the audio file as in-use (permanent)
        if (request.FileId > 0)
        {
            var success = await _fileManagerClient.ChangeTempStatusAsync(request.FileId, "Song", song.Id.ToString(), true, _tenantId, cancellationToken);
            if (!success)
            {
                _logger.LogWarning("Failed to mark FileId {FileId} as permanent for Song {SongId}", request.FileId, song.Id);
            }
        }

        var dto = SongDto.MapFrom(song, song.MoodTags?.Select(t => t.Tag).ToList() ?? []);
        await _fileManagerHelper.EnrichSongWithFileAsync(dto, cancellationToken);
        return dto;
    }
}
