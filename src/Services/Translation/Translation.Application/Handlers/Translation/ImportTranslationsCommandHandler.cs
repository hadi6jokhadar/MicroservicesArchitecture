using IhsanDev.Shared.Application.Exceptions;
using IhsanDev.Shared.Application.Localization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Translation.Application.Commands;
using Translation.Application.Interfaces;
using Translation.Domain.Entities;
using Translation.Domain.Repositories;

namespace Translation.Application.Handlers.Translation;

public class ImportTranslationsCommandHandler : IRequestHandler<ImportTranslationsCommand, ImportTranslationsResult>
{
    private readonly ITranslationKeyRepository _keyRepository;
    private readonly ITranslationValueRepository _valueRepository;
    private readonly ITranslationCacheInvalidator _cacheInvalidator;
    private readonly ILogger<ImportTranslationsCommandHandler> _logger;

    public ImportTranslationsCommandHandler(
        ITranslationKeyRepository keyRepository,
        ITranslationValueRepository valueRepository,
        ITranslationCacheInvalidator cacheInvalidator,
        ILogger<ImportTranslationsCommandHandler> logger)
    {
        _keyRepository = keyRepository;
        _valueRepository = valueRepository;
        _cacheInvalidator = cacheInvalidator;
        _logger = logger;
    }
    
    public async Task<ImportTranslationsResult> Handle(ImportTranslationsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            int createdKeys = 0;
            int updatedValues = 0;

            // In-batch caches to avoid duplicate inserts when the same key/value appears
            // more than once in the import payload.
            var addedKeysBatch = new Dictionary<string, TranslationKey>(StringComparer.OrdinalIgnoreCase);
            var addedValuesBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Every distinct effective tenantId seen in this batch — a key can carry its own
            // #tenantId# prefix that overrides the command-level TenantId, so the cache
            // invalidation below must cover each one, not just request.TenantId. `null` means
            // a global key, tracked alongside actual tenant ids in the same set.
            var affectedTenantIds = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (rawKey, value) in request.Translations)
            {
                // Detect tenant-specific key pattern: #tenantId#.some.key
                // Extract tenantId from key, but keep the full key string as-is
                string? keyTenantId = null;
                if (rawKey.StartsWith('#'))
                {
                    var endHash = rawKey.IndexOf('#', 1);
                    if (endHash > 1)
                    {
                        keyTenantId = rawKey.Substring(1, endHash - 1);
                    }
                }

                // Fall back to the command-level tenantId if the key doesn't carry its own
                var effectiveTenantId = keyTenantId ?? request.TenantId;
                affectedTenantIds.Add(effectiveTenantId);

                // Composite cache key: "tenantId|rawKey" — distinguishes global vs tenant keys
                var batchCacheKey = $"{effectiveTenantId ?? string.Empty}|{rawKey}";

                // Get or create translation key (scoped by tenantId)
                TranslationKey? translationKey;
                if (addedKeysBatch.TryGetValue(batchCacheKey, out var cachedKey))
                {
                    translationKey = cachedKey;
                }
                else
                {
                    translationKey = await _keyRepository.GetByKeyAsync(rawKey, effectiveTenantId, cancellationToken);
                    if (translationKey == null)
                    {
                        translationKey = effectiveTenantId == null
                            ? TranslationKey.Create(rawKey, request.Category, null)
                            : TranslationKey.CreateForTenant(rawKey, request.Category, effectiveTenantId, null);
                        await _keyRepository.AddAsync(translationKey, cancellationToken);
                        addedKeysBatch[batchCacheKey] = translationKey;
                        createdKeys++;
                    }
                }

                // Composite value cache key: "tenantId|keyId|language"
                var valueCacheKey = $"{effectiveTenantId ?? string.Empty}|{translationKey.Id}|{request.Language}";

                // Get or create translation value — skip if already inserted in this batch
                if (!addedValuesBatch.Contains(valueCacheKey))
                {
                    var translationValue = await _valueRepository.GetByKeyLanguageTenantAsync(
                        translationKey.Id,
                        request.Language,
                        effectiveTenantId,
                        cancellationToken);

                    if (translationValue == null)
                    {
                        var newValue = effectiveTenantId == null
                            ? TranslationValue.CreateGlobal(translationKey.Id, request.Language, value)
                            : TranslationValue.CreateTenantOverride(translationKey.Id, request.Language, value, effectiveTenantId);

                        try
                        {
                            await _valueRepository.AddAsync(newValue, cancellationToken);
                            addedValuesBatch.Add(valueCacheKey);
                        }
                        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
                        {
                            // Concurrent insert won the race — fetch and update instead
                            _logger.LogWarning("Concurrent insert detected for value ({KeyId}, {Language}, {TenantId}), retrying as update",
                                translationKey.Id, request.Language, effectiveTenantId);
                            var existing = await _valueRepository.GetByKeyLanguageTenantAsync(
                                translationKey.Id, request.Language, effectiveTenantId, cancellationToken);
                            if (existing != null)
                            {
                                existing.UpdateValue(value);
                                await _valueRepository.UpdateAsync(existing, cancellationToken);
                            }
                        }
                    }
                    else
                    {
                        translationValue.UpdateValue(value);
                        await _valueRepository.UpdateAsync(translationValue, cancellationToken);
                    }
                }

                updatedValues++;
            }
            
            // Invalidate cache for every tenant actually touched by this batch — not just
            // request.TenantId, since individual keys can carry their own #tenantId# prefix
            // (see affectedTenantIds above). A `null` entry (global key) flushes every
            // tenant's cached merged response for this language, not just the global bucket —
            // see ITranslationCacheInvalidator.
            foreach (var tenantId in affectedTenantIds)
            {
                await _cacheInvalidator.InvalidateAsync(request.Language, tenantId, request.Category, cancellationToken);
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

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        // SqlState 23505 = unique_violation in PostgreSQL (covers both PK and unique index conflicts)
        var inner = ex.InnerException;
        while (inner != null)
        {
            if (inner.Message.Contains("23505") || inner.Message.Contains("duplicate key"))
                return true;
            inner = inner.InnerException;
        }
        return false;
    }
}
