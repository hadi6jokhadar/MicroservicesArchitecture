using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Translation.Application.Commands;
using Translation.Domain.Repositories;
using Translation.Domain.Entities;

namespace Translation.Application.Handlers.Translation;

public class DeleteTranslationKeyCommandHandler : IRequestHandler<DeleteTranslationKeyCommand, bool>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly IDistributedCache _cache;
    private readonly ILocalizationService _localizationService;

    public DeleteTranslationKeyCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        IDistributedCache cache,
        ILocalizationService localizationService)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cache = cache;
        _localizationService = localizationService;
    }

    public async Task<bool> Handle(DeleteTranslationKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await _keyRepository.GetByIdAsync(request.Id, cancellationToken);
        if (key == null)
        {
            throw new NotFoundException(
                LocalizationKeys.Exceptions.TranslationKeyNotFound,
                _localizationService);
        }

        // Get all translation values for this key to know which caches to invalidate
        var translationValues = await _valueRepository.GetByKeyIdAsync(key.Id, cancellationToken);
        
        // If already archived, do a hard delete (permanent removal)
        // Otherwise, do a soft delete (set IsArchived = true)
        if (key.IsArchived)
        {
            // Hard delete: Remove from database permanently
            await _keyRepository.HardDeleteAsync(key, cancellationToken);
        }
        else
        {
            // Soft delete: Set IsArchived = true
            await _keyRepository.DeleteAsync(key, cancellationToken);
        }
        
        // Invalidate cache for all languages and tenants that had this translation
        // Cache key pattern: translations:{language}:{tenantId}:{category}
        var clearedCacheKeys = new HashSet<string>();
        foreach (var translationValue in translationValues)
        {
            var tenantKey = translationValue.TenantId ?? "global";
            var cacheKeys = new[]
            {
                $"translations:{translationValue.Language}:{tenantKey}:all",
                $"translations:{translationValue.Language}:{tenantKey}:{key.Category}"
            };
            
            foreach (var cacheKey in cacheKeys)
            {
                if (clearedCacheKeys.Add(cacheKey))
                {
                    await _cache.RemoveAsync(cacheKey, cancellationToken);
                }
            }
        }
        
        return true;
    }
}
