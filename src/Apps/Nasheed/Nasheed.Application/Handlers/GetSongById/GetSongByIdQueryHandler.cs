using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetSongById;

public class GetSongByIdQueryHandler : IRequestHandler<GetSongByIdQuery, SongDto?>
{
    private readonly ISongRepository _songRepository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;

    public GetSongByIdQueryHandler(
        ISongRepository songRepository,
        NasheedFileManagerHelper fileManagerHelper)
    {
        _songRepository = songRepository;
        _fileManagerHelper = fileManagerHelper;
    }

    public async Task<SongDto?> Handle(GetSongByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _songRepository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null) return null;

        var tags = entity.MoodTags?.Select(t => t.Tag).ToList() ?? [];
        var dto = SongDto.MapFrom(entity, tags);
        await _fileManagerHelper.EnrichSongWithFileAsync(dto, cancellationToken);
        return dto;
    }
}
