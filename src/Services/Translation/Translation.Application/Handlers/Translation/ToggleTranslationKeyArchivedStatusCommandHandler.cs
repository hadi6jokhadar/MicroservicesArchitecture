using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ToggleTranslationKeyArchivedStatusCommandHandler> _logger;

    public ToggleTranslationKeyArchivedStatusCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        IDistributedCache cache,
        ILocalizationService localizationService,
        ILogger<ToggleTranslationKeyArchivedStatusCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cache = cache;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<TranslationKeyDto> Handle(ToggleTranslationKeyArchivedStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var key = await _keyRepository.GetByIdWithArchivedAsync(request.Id, cancellationToken);
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
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while toggling archived status for translation key {KeyId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
