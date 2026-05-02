using MediatR;
using Nasheed.Application.DTOs;

namespace Nasheed.Application.Queries;

public record SearchSongsQuery(
    string Query,
    int TopN = 10
) : IRequest<List<SearchResultDto>>;

public record GetSimilarSongsQuery(
    int SongId,
    int TopN = 10
) : IRequest<List<SearchResultDto>>;
