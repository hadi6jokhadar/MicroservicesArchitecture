using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

/// <summary>
/// Handler for toggling translation key archived status
/// </summary>
public class ToggleTranslationKeyArchivedStatusCommandHandler : IRequestHandler<ToggleTranslationKeyArchivedStatusCommand, TranslationKeyDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly IDistributedCache _cache;
    private readonly ILocalizationService _localizationService;

    public ToggleTranslationKeyArchivedStatusCommandHandler(
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

    public async Task<TranslationKeyDto> Handle(ToggleTranslationKeyArchivedStatusCommand request, CancellationToken cancellationToken)
    {
        var key = await _keyRepository.GetByIdAsync(request.Id, cancellationToken);
        if (key == null)
        {
            throw new NotFoundException(
                LocalizationKeys.Exceptions.TranslationKeyNotFound,
                _localizationService);
        }

        key.IsArchived = !key.IsArchived;
        key.LastModified = DateTime.UtcNow;

        await _keyRepository.UpdateAsync(key, cancellationToken);

        // Invalidate cache for all languages and tenants that had this translation
        var translationValues = await _valueRepository.GetByKeyIdAsync(key.Id, cancellationToken);
        
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

        return TranslationKeyDto.MapFrom(key);
    }
}
