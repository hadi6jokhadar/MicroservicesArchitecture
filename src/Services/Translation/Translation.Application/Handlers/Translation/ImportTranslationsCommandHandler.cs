using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Domain.Entities;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class ImportTranslationsCommandHandler : IRequestHandler<ImportTranslationsCommand, ImportTranslationsResult>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ImportTranslationsCommandHandler> _logger;
    
    public ImportTranslationsCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        IDistributedCache cache,
        ILogger<ImportTranslationsCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<ImportTranslationsResult> Handle(ImportTranslationsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            int createdKeys = 0;
            int updatedValues = 0;
            
            foreach (var (key, value) in request.Translations)
            {
                // Get or create translation key
                var translationKey = await _keyRepository.GetByKeyAsync(key, cancellationToken);
                if (translationKey == null)
                {
                    translationKey = TranslationKey.Create(key, request.Category, null);
                    await _keyRepository.AddAsync(translationKey, cancellationToken);
                    createdKeys++;
                }
                
                // Get or create translation value
                var translationValue = await _valueRepository.GetByKeyLanguageTenantAsync(
                    translationKey.Id,
                    request.Language,
                    request.TenantId,
                    cancellationToken);
                
                if (translationValue == null)
                {
                    translationValue = request.TenantId == null
                        ? TranslationValue.CreateGlobal(translationKey.Id, request.Language, value)
                        : TranslationValue.CreateTenantOverride(translationKey.Id, request.Language, value, request.TenantId);
                    
                    await _valueRepository.AddAsync(translationValue, cancellationToken);
                }
                else
                {
                    translationValue.UpdateValue(value);
                    await _valueRepository.UpdateAsync(translationValue, cancellationToken);
                }
                
                updatedValues++;
            }
            
            // Invalidate cache - Clear all translation caches for this language and tenant
            // Since we don't know which categories exist, we need to clear the base cache keys
            // Cache key pattern: translations:{language}:{tenantId}:{category}
            var cacheKeys = new[]
            {
                $"translations:{request.Language}:{request.TenantId ?? "global"}:all",
                $"translations:{request.Language}:{request.TenantId ?? "global"}:{request.Category}"
            };
            
            foreach (var key in cacheKeys)
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            
            return new ImportTranslationsResult(
                request.Translations.Count,
                createdKeys,
                updatedValues,
                $"{updatedValues} translations imported, {createdKeys} new keys created"
            );
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while importing translations");
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
