using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class DeleteTranslationValueCommandHandler : IRequestHandler<DeleteTranslationValueCommand, bool>
{
    private readonly ITranslationValueRepository _valueRepository;
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly IDistributedCache _cache;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<DeleteTranslationValueCommandHandler> _logger;

    public DeleteTranslationValueCommandHandler(
        ITranslationValueRepository valueRepository,
        ITranslationKeyRepository keyRepository,
        IDistributedCache cache,
        ILocalizationService localizationService,
        ILogger<DeleteTranslationValueCommandHandler> logger)
    {
        _valueRepository = valueRepository;
        _keyRepository = keyRepository;
        _cache = cache;
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

            // Invalidate cache for the specific language/tenant/category combination
            // Cache key pattern: translations:{language}:{tenantId}:{category}
            var tenantKey = translationValue.TenantId ?? "global";
            var cacheKeys = new[]
            {
                $"translations:{translationValue.Language}:{tenantKey}:all",
                $"translations:{translationValue.Language}:{tenantKey}:{translationKey.Category}"
            };

            foreach (var cacheKey in cacheKeys)
            {
                await _cache.RemoveAsync(cacheKey, cancellationToken);
            }

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
