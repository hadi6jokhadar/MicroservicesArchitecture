using MediatR;
using Microsoft.Extensions.Caching.Distributed;
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
    
    public SetTranslationCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        IDistributedCache cache)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cache = cache;
    }
    
    public async Task<TranslationValueDto> Handle(SetTranslationCommand request, CancellationToken cancellationToken)
    {
        // Get or create translation key
        var key = await _keyRepository.GetByKeyAsync(request.Key, cancellationToken);
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
}
