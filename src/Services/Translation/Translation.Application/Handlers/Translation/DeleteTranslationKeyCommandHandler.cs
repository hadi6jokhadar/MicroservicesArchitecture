using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.Interfaces;
using Translation.Domain.Repositories;
using Translation.Domain.Entities;

namespace Translation.Application.Handlers.Translation;

public class DeleteTranslationKeyCommandHandler : IRequestHandler<DeleteTranslationKeyCommand, bool>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly ITranslationCacheInvalidator _cacheInvalidator;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<DeleteTranslationKeyCommandHandler> _logger;

    public DeleteTranslationKeyCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        ITranslationCacheInvalidator cacheInvalidator,
        ILocalizationService localizationService,
        ILogger<DeleteTranslationKeyCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cacheInvalidator = cacheInvalidator;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteTranslationKeyCommand request, CancellationToken cancellationToken)
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

            // Get all translation values for this key to know which caches to invalidate
            var translationValues = await _valueRepository.GetByKeyIdAsync(key.Id, cancellationToken);
            
            // If already archived, do a hard delete (permanent removal)
            // Otherwise, do a soft delete (set IsArchived = true)
            if (key.IsArchived)
            {
                // Hard delete: Remove from database permanently
                await _keyRepository.HardDeleteAsync(key, cancellationToken);
            }
            else
            {
                // Soft delete: Set IsArchived = true
                await _keyRepository.DeleteAsync(key, cancellationToken);
            }
            
            // Invalidate cache for every distinct language/tenant this key had a value for.
            // A null TenantId (global) flushes every tenant's cached merged response for that
            // language — see ITranslationCacheInvalidator — so dedupe on (language, tenantId)
            // only, not per-cache-key, to avoid redundant pattern-scan flushes.
            var invalidated = new HashSet<(string Language, string? TenantId)>();
            foreach (var translationValue in translationValues)
            {
                if (invalidated.Add((translationValue.Language, translationValue.TenantId)))
                {
                    await _cacheInvalidator.InvalidateAsync(
                        translationValue.Language, translationValue.TenantId, key.Category, cancellationToken);
                }
            }

            return true;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting translation key {KeyId}", request.Id);
            throw new GeneralException(LocalizationKeys.Exceptions.InternalServerError);
        }
    }
}
