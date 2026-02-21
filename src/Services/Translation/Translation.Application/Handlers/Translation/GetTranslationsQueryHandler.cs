using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Translation.Application.DTOs;
using Translation.Application.Queries;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class GetTranslationsQueryHandler : IRequestHandler<GetTranslationsQuery, TranslationsDto>
{
    private readonly ITranslationValueRepository _valueRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GetTranslationsQueryHandler> _logger;
    
    public GetTranslationsQueryHandler(
        ITranslationValueRepository valueRepository,
        IDistributedCache cache,
        ILogger<GetTranslationsQueryHandler> logger)
    {
        _valueRepository = valueRepository;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<TranslationsDto> Handle(GetTranslationsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"translations:{request.Language}:{request.TenantId ?? "global"}:{request.Category ?? "all"}";
            
            // Try to get from cache
            var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedDto = JsonSerializer.Deserialize<TranslationsDto>(cached);
                if (cachedDto != null)
                {
                    return cachedDto;
                }
            }
            
            // Get from database
            var values = await _valueRepository.GetByLanguageAsync(
                request.Language, 
                request.TenantId, 
                request.Category,
                cancellationToken);
            
            // When both global and tenant-specific values exist for the same key, prioritize tenant-specific
            var translations = values
                .Where(v => v.TranslationKey != null && v.TranslationKey.Key != null)
                .GroupBy(v => v.TranslationKey.Key)
                .Select(g => g.OrderByDescending(v => v.TenantId != null).First())
                .ToDictionary(v => v.TranslationKey.Key, v => v.Value);
            
            var result = new TranslationsDto
            {
                Language = request.Language,
                TenantId = request.TenantId,
                Translations = translations,
                CachedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            
            // Cache for 1 hour
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                options,
                cancellationToken);
            
            return result;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting translations for language {Language} and tenant {TenantId}", request.Language, request.TenantId);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
