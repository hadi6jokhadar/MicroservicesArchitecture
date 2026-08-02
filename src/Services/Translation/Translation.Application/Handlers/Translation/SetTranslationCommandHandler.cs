using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Application.Interfaces;
using Translation.Domain.Entities;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class SetTranslationCommandHandler : IRequestHandler<SetTranslationCommand, TranslationValueDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly ITranslationCacheInvalidator _cacheInvalidator;
    private readonly ILogger<SetTranslationCommandHandler> _logger;

    public SetTranslationCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        ITranslationCacheInvalidator cacheInvalidator,
        ILogger<SetTranslationCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cacheInvalidator = cacheInvalidator;
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
            
            // A tenantId of null means this write touched a global value, which every tenant's
            // cached merged response falls back to — see ITranslationCacheInvalidator.
            await _cacheInvalidator.InvalidateAsync(request.Language, request.TenantId, request.Category, cancellationToken);

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
