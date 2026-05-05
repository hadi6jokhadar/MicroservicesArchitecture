using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Domain.Entities;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class CreateTranslationKeyCommandHandler : IRequestHandler<CreateTranslationKeyCommand, TranslationKeyDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<CreateTranslationKeyCommandHandler> _logger;
    
    public CreateTranslationKeyCommandHandler(
        ITranslationKeyRepository keyRepository,
        ILocalizationService localizationService,
        ILogger<CreateTranslationKeyCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _localizationService = localizationService;
        _logger = logger;
    }
    
    public async Task<TranslationKeyDto> Handle(CreateTranslationKeyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if key already exists (global scope — no tenantId for manually created keys)
            var exists = await _keyRepository.KeyExistsAsync(request.Key, null, cancellationToken);
            if (exists)
            {
                throw new ConflictException(LocalizationKeys.Exceptions.TranslationKeyAlreadyExists, _localizationService, request.Key);
            }
            
            // Create new translation key
            var key = TranslationKey.Create(request.Key, request.Category, request.Description);
            
            await _keyRepository.AddAsync(key, cancellationToken);
            
            return TranslationKeyDto.MapFrom(key);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating translation key {Key}", request.Key);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
