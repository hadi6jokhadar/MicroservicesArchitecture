using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.Interfaces;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class DeleteTranslationValueCommandHandler : IRequestHandler<DeleteTranslationValueCommand, bool>
{
    private readonly ITranslationValueRepository _valueRepository;
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationCacheInvalidator _cacheInvalidator;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<DeleteTranslationValueCommandHandler> _logger;

    public DeleteTranslationValueCommandHandler(
        ITranslationValueRepository valueRepository,
        ITranslationKeyRepository keyRepository,
        ITranslationCacheInvalidator cacheInvalidator,
        ILocalizationService localizationService,
        ILogger<DeleteTranslationValueCommandHandler> logger)
    {
        _valueRepository = valueRepository;
        _keyRepository = keyRepository;
        _cacheInvalidator = cacheInvalidator;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteTranslationValueCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var translationValue = await _valueRepository.GetByIdWithArchivedAsync(request.Id, cancellationToken);
            if (translationValue == null)
            {
                throw new NotFoundException(
                    LocalizationKeys.Exceptions.TranslationValueNotFound,
                    _localizationService);
            }

            // Get the translation key to retrieve category for cache invalidation
            var translationKey = await _keyRepository.GetByIdWithArchivedAsync(translationValue.TranslationKeyId, cancellationToken);
            if (translationKey == null)
            {
                throw new NotFoundException(
                    LocalizationKeys.Exceptions.TranslationKeyNotFound,
                    _localizationService);
            }

            if (translationValue.IsArchived)
            {
                await _valueRepository.HardDeleteAsync(translationValue, cancellationToken);
            }
            else
            {
                await _valueRepository.DeleteAsync(translationValue, cancellationToken);
            }

            // A null TenantId means this was a global value, which every tenant's cached
            // merged response falls back to — see ITranslationCacheInvalidator.
            await _cacheInvalidator.InvalidateAsync(
                translationValue.Language, translationValue.TenantId, translationKey.Category, cancellationToken);

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting translation value {ValueId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
