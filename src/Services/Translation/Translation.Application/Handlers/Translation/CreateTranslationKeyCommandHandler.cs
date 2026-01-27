using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Domain.Entities;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class CreateTranslationKeyCommandHandler : IRequestHandler<CreateTranslationKeyCommand, TranslationKeyDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ILocalizationService _localizationService;
    
    public CreateTranslationKeyCommandHandler(
        ITranslationKeyRepository keyRepository,
        ILocalizationService localizationService)
    {
        _keyRepository = keyRepository;
        _localizationService = localizationService;
    }
    
    public async Task<TranslationKeyDto> Handle(CreateTranslationKeyCommand request, CancellationToken cancellationToken)
    {
        // Check if key already exists
        var exists = await _keyRepository.KeyExistsAsync(request.Key, cancellationToken);
        if (exists)
        {
            throw new ConflictException(LocalizationKeys.Exceptions.TranslationKeyAlreadyExists, _localizationService, request.Key);
        }
        
        // Create new translation key
        var key = TranslationKey.Create(request.Key, request.Category, request.Description);
        
        await _keyRepository.AddAsync(key, cancellationToken);
        
        return TranslationKeyDto.MapFrom(key);
    }
}
