using MediatR;
using Nasheed.Application.DTOs;
using Nasheed.Application.Queries;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.GetSongAnalysisStatus;

public class GetSongAnalysisStatusQueryHandler : IRequestHandler<GetSongAnalysisStatusQuery, SongDto?>
{
    private readonly ISongRepository _songRepository;
    private readonly ISongMoodTagRepository _moodTagRepository;

    public GetSongAnalysisStatusQueryHandler(ISongRepository songRepository, ISongMoodTagRepository moodTagRepository)
    {
        _songRepository = songRepository;
        _moodTagRepository = moodTagRepository;
    }

    public async Task<SongDto?> Handle(GetSongAnalysisStatusQuery request, CancellationToken cancellationToken)
    {
        var entity = await _songRepository.GetByIdAsync(request.SongId, cancellationToken);
        if (entity == null) return null;

        var tags = await _moodTagRepository.GetBySongIdAsync(entity.Id, cancellationToken);
        return SongDto.MapFrom(entity, tags.Select(t => t.Tag).ToList());
    }
}
