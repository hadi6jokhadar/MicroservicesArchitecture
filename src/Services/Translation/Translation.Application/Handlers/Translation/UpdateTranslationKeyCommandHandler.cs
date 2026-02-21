using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class UpdateTranslationKeyCommandHandler : IRequestHandler<UpdateTranslationKeyCommand, TranslationKeyDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<UpdateTranslationKeyCommandHandler> _logger;

    public UpdateTranslationKeyCommandHandler(
        ITranslationKeyRepository keyRepository,
        ILocalizationService localizationService,
        ILogger<UpdateTranslationKeyCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<TranslationKeyDto> Handle(UpdateTranslationKeyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var key = await _keyRepository.GetByIdAsync(request.Id, cancellationToken);
            if (key == null)
            {
                throw new NotFoundException(
                    LocalizationKeys.Exceptions.TranslationKeyNotFound,
                    _localizationService);
            }

            key.Update(request.Description);
            await _keyRepository.UpdateAsync(key, cancellationToken);

            return TranslationKeyDto.MapFrom(key);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating translation key {KeyId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
