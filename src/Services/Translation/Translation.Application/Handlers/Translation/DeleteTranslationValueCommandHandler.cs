using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Translation.Application.Commands;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class DeleteTranslationValueCommandHandler : IRequestHandler<DeleteTranslationValueCommand, bool>
{
    private readonly ITranslationValueRepository _valueRepository;
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly IDistributedCache _cache;
    private readonly ILocalizationService _localizationService;

    public DeleteTranslationValueCommandHandler(
        ITranslationValueRepository valueRepository,
        ITranslationKeyRepository keyRepository,
        IDistributedCache cache,
        ILocalizationService localizationService)
    {
        _valueRepository = valueRepository;
        _keyRepository = keyRepository;
        _cache = cache;
        _localizationService = localizationService;
    }

    public async Task<bool> Handle(DeleteTranslationValueCommand request, CancellationToken cancellationToken)
    {
        var translationValue = await _valueRepository.GetByIdAsync(request.Id, cancellationToken);
        if (translationValue == null)
        {
            throw new NotFoundException(
                LocalizationKeys.Exceptions.TranslationValueNotFound,
                _localizationService);
        }

        // Get the translation key to retrieve category for cache invalidation
        var translationKey = await _keyRepository.GetByIdAsync(translationValue.TranslationKeyId, cancellationToken);
        if (translationKey == null)
        {
            throw new NotFoundException(
                LocalizationKeys.Exceptions.TranslationKeyNotFound,
                _localizationService);
        }

        await _valueRepository.DeleteAsync(translationValue, cancellationToken);

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
}
