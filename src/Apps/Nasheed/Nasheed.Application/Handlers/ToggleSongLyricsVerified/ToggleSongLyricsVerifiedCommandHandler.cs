using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Nasheed.Application.Commands;
using Nasheed.Application.DTOs;
using Nasheed.Application.Helpers;
using Nasheed.Domain.Interfaces;

namespace Nasheed.Application.Handlers.ToggleSongLyricsVerified;

public class ToggleSongLyricsVerifiedCommandHandler : IRequestHandler<ToggleSongLyricsVerifiedCommand, SongDto>
{
    private readonly ISongRepository _repository;
    private readonly NasheedFileManagerHelper _fileManagerHelper;
    private readonly ILogger<ToggleSongLyricsVerifiedCommandHandler> _logger;

    public ToggleSongLyricsVerifiedCommandHandler(
        ISongRepository repository,
        NasheedFileManagerHelper fileManagerHelper,
        ILogger<ToggleSongLyricsVerifiedCommandHandler> logger)
    {
        _repository = repository;
        _fileManagerHelper = fileManagerHelper;
        _logger = logger;
    }

    public async Task<SongDto> Handle(ToggleSongLyricsVerifiedCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(LocalizationKeys.Exceptions.SongNotFound);

            entity.SetLyricsVerified(!entity.LyricsVerified);

            await _repository.UpdateAsync(entity, cancellationToken);

            _logger.LogInformation("Toggled LyricsVerified for Song Id {Id} to {LyricsVerified}", entity.Id, entity.LyricsVerified);

            var dto = SongDto.MapFrom(entity, entity.MoodTags?.Select(t => t.Tag).ToList() ?? []);
            await _fileManagerHelper.EnrichSongWithFileAsync(dto, cancellationToken);
            return dto;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while toggling LyricsVerified for Song Id {Id}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
