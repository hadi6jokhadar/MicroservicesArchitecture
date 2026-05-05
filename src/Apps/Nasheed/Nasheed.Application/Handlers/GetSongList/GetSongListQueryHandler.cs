using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetSongList;

public class GetSongListQueryHandler : IRequestHandler<GetSongListQuery, PaginatedList<SongDto>>
{
    private readonly ISongRepository _repository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;

    public GetSongListQueryHandler(ISongRepository repository, NasheedFileManagerHelper fileManagerHelper)
    {
        _repository = repository;
        _fileManagerHelper = fileManagerHelper;
    }

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

        var dtos = items.Select(s => SongDto.MapFrom(s)).ToList();
        await _fileManagerHelper.EnrichSongsWithFilesAsync(dtos, cancellationToken);

        return new PaginatedList<SongDto>
        {
            Items = dtos,
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
