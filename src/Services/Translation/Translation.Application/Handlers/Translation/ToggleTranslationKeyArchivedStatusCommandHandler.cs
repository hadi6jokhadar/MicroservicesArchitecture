using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.DTOs;
using Translation.Application.Interfaces;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

/// <summary>
/// Handler for toggling translation key archived status
/// </summary>
public class ToggleTranslationKeyArchivedStatusCommandHandler : IRequestHandler<ToggleTranslationKeyArchivedStatusCommand, TranslationKeyDto>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly ITranslationCacheInvalidator _cacheInvalidator;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<ToggleTranslationKeyArchivedStatusCommandHandler> _logger;

    public ToggleTranslationKeyArchivedStatusCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        ITranslationCacheInvalidator cacheInvalidator,
        ILocalizationService localizationService,
        ILogger<ToggleTranslationKeyArchivedStatusCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cacheInvalidator = cacheInvalidator;
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

            // Invalidate cache for every distinct language/tenant this key had a value for.
            // A null TenantId (global) flushes every tenant's cached merged response for that
            // language — see ITranslationCacheInvalidator — so dedupe on (language, tenantId)
            // only, not per-cache-key, to avoid redundant pattern-scan flushes.
            var translationValues = await _valueRepository.GetByKeyIdAsync(key.Id, cancellationToken);

            var invalidated = new HashSet<(string Language, string? TenantId)>();
            foreach (var translationValue in translationValues)
            {
                if (invalidated.Add((translationValue.Language, translationValue.TenantId)))
                {
                    await _cacheInvalidator.InvalidateAsync(
                        translationValue.Language, translationValue.TenantId, key.Category, cancellationToken);
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
