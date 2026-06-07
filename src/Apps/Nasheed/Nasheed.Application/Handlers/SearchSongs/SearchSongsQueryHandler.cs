using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Constants;
using Polly.CircuitBreaker;
using Nasheed.Application.DTOs;
using Nasheed.Application.Interfaces;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.SearchSongs;

public class SearchSongsQueryHandler : IRequestHandler<SearchSongsQuery, List<SearchResultDto>>
{
    private readonly IAiApiClient _aiClient;
    private readonly ISongSearchDocumentRepository _searchDocRepository;
    private readonly ISongRepository _songRepository;
    private readonly ILogger<SearchSongsQueryHandler> _logger;

    public SearchSongsQueryHandler(
        IAiApiClient aiClient,
        ISongSearchDocumentRepository searchDocRepository,
        ISongRepository songRepository,
        ILogger<SearchSongsQueryHandler> logger)
    {
        _aiClient = aiClient;
        _searchDocRepository = searchDocRepository;
        _songRepository = songRepository;
        _logger = logger;
    }

    public async Task<List<SearchResultDto>> Handle(SearchSongsQuery request, CancellationToken cancellationToken)
    {
        var normalizedQuery = request.Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            _logger.LogDebug("Empty search query received; returning empty result set");
            return new List<SearchResultDto>();
        }

        var topN = request.TopN > 0 ? request.TopN : 10;

        var directMatchIds = await _searchDocRepository.SearchByTextAsync(normalizedQuery, topN, cancellationToken);
        if (directMatchIds.Count > 0)
        {
            var directSongs = await _songRepository.GetByIdsAsync(directMatchIds, cancellationToken);
            var directSongMap = directSongs.ToDictionary(s => s.Id);

            var directResults = directMatchIds
                .Where(directSongMap.ContainsKey)
                .Select(id =>
                {
                    var song = directSongMap[id];
                    return new SearchResultDto
                    {
                        SongId = song.Id,
                        Title = song.Title,
                        ArtistName = song.Artist?.Name,
                        Summary = song.Summary,
                        VocalStyle = song.VocalStyle,
                        MoodTags = new List<string>(),
                        Score = 1.0
                    };
                })
                .ToList();

            _logger.LogInformation("Search '{Query}' returned {Count} lexical matches without embedding", normalizedQuery, directResults.Count);
            return directResults;
        }

        float[] queryEmbedding;
        try
        {
            queryEmbedding = await _aiClient.EmbedAsync(
                NasheedAiKeys.EmbeddingSettings,
                normalizedQuery,
                cancellationToken: cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "AI circuit open; semantic search unavailable for query '{Query}'", normalizedQuery);
            return new List<SearchResultDto>();
        }

        var similarPairs = await _searchDocRepository.SearchSimilarAsync(queryEmbedding, topN, cancellationToken);
        if (similarPairs.Count == 0) return new List<SearchResultDto>();

        var songIds = similarPairs.Select(p => p.SongId).ToList();
        var songs = await _songRepository.GetByIdsAsync(songIds, cancellationToken);
        var songMap = songs.ToDictionary(s => s.Id);

        var results = similarPairs
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

        _logger.LogInformation("Search '{Query}' returned {Count} results", normalizedQuery, results.Count);
        return results;
    }
}
