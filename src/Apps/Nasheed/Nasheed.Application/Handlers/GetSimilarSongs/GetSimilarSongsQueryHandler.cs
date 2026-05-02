using IhsanDev.Shared.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetSimilarSongs;

public class GetSimilarSongsQueryHandler : IRequestHandler<GetSimilarSongsQuery, List<SearchResultDto>>
{
    private readonly ISongSearchDocumentRepository _searchDocRepository;
    private readonly ISongRepository _songRepository;
    private readonly ILogger<GetSimilarSongsQueryHandler> _logger;

    public GetSimilarSongsQueryHandler(
        ISongSearchDocumentRepository searchDocRepository,
        ISongRepository songRepository,
        ILogger<GetSimilarSongsQueryHandler> logger)
    {
        _searchDocRepository = searchDocRepository;
        _songRepository = songRepository;
        _logger = logger;
    }

    public async Task<List<SearchResultDto>> Handle(GetSimilarSongsQuery request, CancellationToken cancellationToken)
    {
        var sourceDoc = await _searchDocRepository.GetBySongIdAsync(request.SongId, cancellationToken)
            ?? throw new NotFoundException($"No search document found for Song Id '{request.SongId}'. The song may not have been indexed yet.");

        // Deserialize stored embedding
        var embedding = System.Text.Json.JsonSerializer.Deserialize<float[]>(sourceDoc.EmbeddingJson)
            ?? throw new InvalidOperationException("Failed to deserialize stored embedding.");

        // Exclude the source song from results
        var similarPairs = await _searchDocRepository.SearchSimilarAsync(embedding, request.TopN + 1, cancellationToken);
        var filtered = similarPairs.Where(p => p.SongId != request.SongId).Take(request.TopN).ToList();

        if (filtered.Count == 0) return new List<SearchResultDto>();

        var songIds = filtered.Select(p => p.SongId).ToList();
        var songs = await _songRepository.GetByIdsAsync(songIds, cancellationToken);
        var songMap = songs.ToDictionary(s => s.Id);

        var results = filtered
            .Where(p => songMap.ContainsKey(p.SongId))
            .Select(p =>
            {
                var song = songMap[p.SongId];
                return new SearchResultDto
                {
                    SongId = song.Id,
                    Title = song.Title,
                    ArtistName = song.Artist?.Name,
                    Summary = song.Summary,
                    VocalStyle = song.VocalStyle,
                    MoodTags = new List<string>(),
                    Score = p.Score
                };
            })
            .ToList();

        _logger.LogInformation("Found {Count} similar songs for Song {SongId}", results.Count, request.SongId);
        return results;
    }
}
