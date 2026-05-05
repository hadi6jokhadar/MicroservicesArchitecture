using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetSongList;

public class GetSongListQueryHandler : IRequestHandler<GetSongListQuery, PaginatedList<SongDto>>
{
    private readonly ISongRepository _repository;

    public GetSongListQueryHandler(ISongRepository repository) => _repository = repository;

    public async Task<PaginatedList<SongDto>> Handle(GetSongListQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetAllAsync(
            request.TextFilter,
            request.ArtistId,
            request.State,
            request.CopyrightRiskLevel,
            request.ContentSafetyFlag,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return new PaginatedList<SongDto>
        {
            Items = items.Select(s => SongDto.MapFrom(s)).ToList(),
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
