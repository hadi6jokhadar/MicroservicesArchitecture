using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Domain.Entities;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class SetTranslationCommandHandler : IRequestHandler<SetTranslationCommand, TranslationValueDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SetTranslationCommandHandler> _logger;
    
    public SetTranslationCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        IDistributedCache cache,
        ILogger<SetTranslationCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<TranslationValueDto> Handle(SetTranslationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get or create translation key
            var key = await _keyRepository.GetByKeyAsync(request.Key, null, cancellationToken);
            if (key == null)
            {
                key = TranslationKey.Create(request.Key, request.Category, null);
                await _keyRepository.AddAsync(key, cancellationToken);
            }
            
            // Get or create translation value
            var value = await _valueRepository.GetByKeyLanguageTenantAsync(
                key.Id, 
                request.Language, 
                request.TenantId, 
                cancellationToken);
            
            if (value == null)
            {
                value = request.TenantId == null
                    ? TranslationValue.CreateGlobal(key.Id, request.Language, request.Value)
                    : TranslationValue.CreateTenantOverride(key.Id, request.Language, request.Value, request.TenantId);
                
                await _valueRepository.AddAsync(value, cancellationToken);
            }
            else
            {
                value.UpdateValue(request.Value);
                await _valueRepository.UpdateAsync(value, cancellationToken);
            }
            
            // Invalidate cache - Clear all translation caches for this language and tenant
            // Cache key pattern: translations:{language}:{tenantId}:{category}
            var cacheKeys = new[]
            {
                $"translations:{request.Language}:{request.TenantId ?? "global"}:all",
                $"translations:{request.Language}:{request.TenantId ?? "global"}:{request.Category}"
            };
            
            foreach (var cacheKeyToRemove in cacheKeys)
            {
                await _cache.RemoveAsync(cacheKeyToRemove, cancellationToken);
            }
            
            return TranslationValueDto.MapFrom(value, key.Key);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while setting translation key {TranslationKey}", request.Key);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
