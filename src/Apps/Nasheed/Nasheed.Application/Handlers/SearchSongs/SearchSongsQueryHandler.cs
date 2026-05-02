using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Constants;
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
        var queryEmbedding = await _aiClient.EmbedAsync(
            NasheedAiKeys.EmbeddingSettings,
            request.Query,
            cancellationToken: cancellationToken);

        var similarPairs = await _searchDocRepository.SearchSimilarAsync(queryEmbedding, request.TopN, cancellationToken);
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

        _logger.LogInformation("Search '{Query}' returned {Count} results", request.Query, results.Count);
        return results;
    }
}
