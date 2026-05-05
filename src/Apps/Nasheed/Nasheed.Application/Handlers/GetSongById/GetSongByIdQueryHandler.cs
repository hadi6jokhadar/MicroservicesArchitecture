using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetSongById;

public class GetSongByIdQueryHandler : IRequestHandler<GetSongByIdQuery, SongDto?>
{
    private readonly ISongRepository _songRepository;
    private readonly ISongMoodTagRepository _moodTagRepository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;

    public GetSongByIdQueryHandler(
        ISongRepository songRepository,
        ISongMoodTagRepository moodTagRepository,
        NasheedFileManagerHelper fileManagerHelper)
    {
        _songRepository = songRepository;
        _moodTagRepository = moodTagRepository;
        _fileManagerHelper = fileManagerHelper;
    }

    public async Task<SongDto?> Handle(GetSongByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _songRepository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null) return null;

        var tags = await _moodTagRepository.GetBySongIdAsync(entity.Id, cancellationToken);
        var dto = SongDto.MapFrom(entity, tags.Select(t => t.Tag).ToList());
        await _fileManagerHelper.EnrichSongWithFileAsync(dto, cancellationToken);
        return dto;
    }
}
