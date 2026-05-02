using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Domain.Entities;
using Nasheed.Domain.Enums;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.CreateSong;

public class CreateSongCommandHandler : IRequestHandler<CreateSongCommand, SongDto>
{
    private readonly ISongRepository _songRepository;
    private readonly IArtistRepository _artistRepository;
    private readonly ISongIngestionJobRepository _ingestionJobRepository;
    private readonly ILogger<CreateSongCommandHandler> _logger;

    public CreateSongCommandHandler(
        ISongRepository songRepository,
        IArtistRepository artistRepository,
        ISongIngestionJobRepository ingestionJobRepository,
        ILogger<CreateSongCommandHandler> logger)
    {
        _songRepository = songRepository;
        _artistRepository = artistRepository;
        _ingestionJobRepository = ingestionJobRepository;
        _logger = logger;
    }

    public async Task<SongDto> Handle(CreateSongCommand request, CancellationToken cancellationToken)
    {
        var artist = await _artistRepository.GetByIdAsync(request.ArtistId, cancellationToken)
            ?? throw new NotFoundException($"Artist with Id '{request.ArtistId}' not found.");

        var song = SongEntity.Create(request.ArtistId, request.Title, request.FileId);
        await _songRepository.AddAsync(song, cancellationToken);

        // Enqueue ingestion job
        song.SetState(SongState.InQueue);
        await _songRepository.UpdateAsync(song, cancellationToken);

        var job = SongIngestionJobEntity.Create(song.Id, request.FileId, IngestionJobType.FullPipeline);
        await _ingestionJobRepository.AddAsync(job, cancellationToken);

        // Increment artist song count
        artist.IncrementSongCount();
        await _artistRepository.UpdateAsync(artist, cancellationToken);

        _logger.LogInformation("Created Song Id {SongId} with ingestion job Id {JobId}", song.Id, job.Id);
        return SongDto.MapFrom(song);
    }
}
