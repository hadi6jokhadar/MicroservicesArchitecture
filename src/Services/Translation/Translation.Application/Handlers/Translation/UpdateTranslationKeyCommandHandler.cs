using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class UpdateTranslationKeyCommandHandler : IRequestHandler<UpdateTranslationKeyCommand, TranslationKeyDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ILocalizationService _localizationService;

    public UpdateTranslationKeyCommandHandler(
        ITranslationKeyRepository keyRepository,
        ILocalizationService localizationService)
    {
        _keyRepository = keyRepository;
        _localizationService = localizationService;
    }

    public async Task<TranslationKeyDto> Handle(UpdateTranslationKeyCommand request, CancellationToken cancellationToken)
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
}
